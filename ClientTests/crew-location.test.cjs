const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { runInNewContext } = require('node:vm');
function setup(geolocation) {
    const nodes = new Map();
    const get = id => { if (!nodes.has(id)) nodes.set(id, { addEventListener(e, fn) { this[e] = fn; } }); return nodes.get(id); };
    const context = { document: { getElementById: get }, navigator: { geolocation } };
    runInNewContext(readFileSync('wwwroot/js/crew-location.js', 'utf8'), context);
    return { get, api: context.CrewTaskLocation };
}
test('location is opt-in and clear cancels late GPS callbacks', () => {
    let success; const state = setup({ getCurrentPosition(fn) { success = fn; } });
    assert.equal(state.api.current(), null);
    state.get('crew-location-capture').click(); state.get('crew-location-clear').click();
    success({ coords: { latitude: 23.7, longitude: 90.3 } }); assert.equal(state.api.current(), null);
});
test('GPS failure does not prevent task updates and valid GPS is exposed only after capture', () => {
    const failed = setup({ getCurrentPosition(ok, fail) { fail(); } }); failed.get('crew-location-capture').click();
    assert.match(failed.get('crew-location-status').textContent, /without location/); assert.equal(failed.api.current(), null);
    const state = setup({ getCurrentPosition(ok) { ok({ coords: { latitude: 23.7, longitude: 90.3 } }); } });
    state.get('crew-location-capture').click(); assert.equal(state.api.current().latitude, 23.7);
});
