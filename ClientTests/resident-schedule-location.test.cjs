const { test } = require('node:test'); const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs'); const { runInNewContext } = require('node:vm');
const geometry={type:'Polygon',coordinates:[[[90,23],[91,23],[91,24],[90,24],[90,23]]]};
function setup(geolocation,features=[{id:1,geometry,properties:{name:'Test zone'}}]) {
    const nodes=new Map(); const get=id=>{if(!nodes.has(id))nodes.set(id,{disabled:false,addEventListener(e,fn){this[e]=fn;}});return nodes.get(id);};
    const context={};runInNewContext(readFileSync('wwwroot/js/zone-locator.js','utf8'),context);
    runInNewContext(readFileSync('wwwroot/js/resident-schedule-location.js','utf8'),context);
    const selected=[];let boundaryRequests=0;
    const api=context.ResidentScheduleLocation.mount({document:{getElementById:get},navigator:{geolocation},locator:context.ZoneLocator,
        getZones:async()=>{boundaryRequests++;return {features};},onSelect:id=>selected.push(id)});
    return {get,api,selected,boundaryRequests:()=>boundaryRequests};
}
test('GPS is click-only and only a containing zone is selected without uploading coordinates',async()=>{
    let calls=0;const s=setup({getCurrentPosition(ok){calls++;ok({coords:{latitude:23.5,longitude:90.5}});}});
    assert.equal(calls,0);assert.equal(s.boundaryRequests(),0);await s.get('schedule-locate').click();
    assert.equal(calls,1);assert.deepEqual(s.selected,[1]);assert.equal(s.boundaryRequests(),1);
});
test('denial and unsupported GPS keep manual selection available',async()=>{
    const s=setup({getCurrentPosition(ok,fail){fail({code:1});}});await s.get('schedule-locate').click();
    assert.match(s.get('schedule-location-status').textContent,/denied/);assert.equal(s.get('schedule-locate').disabled,false);assert.deepEqual(s.selected,[]);
    const missing=setup(undefined);await missing.get('schedule-locate').click();assert.match(missing.get('schedule-location-status').textContent,/unavailable/);
});
test('outside and ambiguous locations never guess a nearest zone',async()=>{
    const outside=setup({getCurrentPosition(ok){ok({coords:{latitude:25,longitude:92}});}});await outside.get('schedule-locate').click();
    assert.deepEqual(outside.selected,[]);assert.match(outside.get('schedule-location-status').textContent,/outside/);
    const overlap=setup({getCurrentPosition(ok){ok({coords:{latitude:23.5,longitude:90.5}});}},[1,2].map(id=>({id,geometry,properties:{name:'Area '+id}})));
    await overlap.get('schedule-locate').click();assert.deepEqual(overlap.selected,[]);assert.match(overlap.get('schedule-location-status').textContent,/multiple/);
});
test('manual change or clear invalidates a late GPS callback',async()=>{
    let success;const s=setup({getCurrentPosition(ok){success=ok;}});await s.get('schedule-locate').click();s.api.cancel();
    success({coords:{latitude:23.5,longitude:90.5}});assert.deepEqual(s.selected,[]);
    await s.get('schedule-locate').click();s.get('schedule-location-clear').click();success({coords:{latitude:23.5,longitude:90.5}});
    assert.deepEqual(s.selected,[]);assert.match(s.get('schedule-location-status').textContent,/cleared/);
});
test('missing boundaries and invalid coordinates keep the selected zone unchanged',async()=>{
    const empty=setup({getCurrentPosition(){assert.fail('Do not ask GPS without boundaries');}},[]);await empty.get('schedule-locate').click();
    assert.match(empty.get('schedule-location-status').textContent,/boundaries are unavailable/);
    const invalid=setup({getCurrentPosition(ok){ok({coords:{latitude:NaN,longitude:90.5}});}});await invalid.get('schedule-locate').click();assert.deepEqual(invalid.selected,[]);
});
