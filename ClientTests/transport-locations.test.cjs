const { test } = require('node:test');
const assert = require('node:assert/strict');
const { create, inside } = require('../wwwroot/js/transport-locations.js');
test('local catalog resolves named landmarks and never guesses arbitrary addresses', async () => {
    let calls = 0;
    const provider = create({ fetcher: async () => { calls++; return { ok: true, json: async () => [{name:'Farmgate',latitude:23.7581,longitude:90.3896},{name:'Bad',latitude:99,longitude:90}] }; } });
    assert.equal(calls,0); assert.equal((await provider.load()).length,1);
    assert.equal(provider.resolve(' farmgate ').name,'Farmgate'); assert.equal(provider.resolve('unknown street'),null);
    assert.equal(inside({latitude:NaN,longitude:90.3}),false);
});
test('GPS is click-only and permission denial/outside area do not silently select another place', async () => {
    let calls = 0;
    const denied = create({navigator:{geolocation:{getCurrentPosition:(_ok,fail)=>{calls++;fail({code:1});}}}});
    assert.equal(calls,0); await assert.rejects(denied.locate(),/permission denied/); assert.equal(calls,1);
    const outside = create({navigator:{geolocation:{getCurrentPosition:ok=>ok({coords:{latitude:24,longitude:90}})}}});
    await assert.rejects(outside.locate(),/outside/);
    const valid = create({navigator:{geolocation:{getCurrentPosition:ok=>ok({coords:{latitude:23.75,longitude:90.37}})}}});
    assert.equal((await valid.locate()).name,'Current location');
});
test('provider errors are retryable and missing GPS is explicit', async()=>{
    let ready=false;
    const provider=create({navigator:{},fetcher:async()=>({ok:ready,json:async()=>[]})});
    await assert.rejects(provider.load(),/Retry/);ready=true;assert.deepEqual(await provider.load(),[]);
    await assert.rejects(provider.locate(),/unavailable/);
});
