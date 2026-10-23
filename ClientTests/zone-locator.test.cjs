const { test } = require('node:test'); const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs'); const { runInNewContext } = require('node:vm');
const context = {}; runInNewContext(readFileSync('wwwroot/js/zone-locator.js','utf8'), context);
const contains = context.ZoneLocator.contains;
const outer = [[0,0],[10,0],[10,10],[0,10],[0,0]], hole = [[3,3],[7,3],[7,7],[3,7],[3,3]];
test('polygon respects longitude/latitude order, boundary and holes', () => {
    const geometry = { type:'Polygon', coordinates:[outer,hole] };
    assert.equal(contains(geometry,1,2),true); assert.equal(contains(geometry,5,5),false);
    assert.equal(contains(geometry,11,5),false); assert.equal(contains(geometry,0,5),true);
});
test('supports multipolygons and rejects malformed/nonfinite coordinates', () => {
    assert.equal(contains({type:'MultiPolygon',coordinates:[[outer]]},2,1),true);
    assert.equal(contains(null,2,1),false); assert.equal(contains({type:'Polygon'},2,1),false);
    assert.equal(contains({type:'Polygon',coordinates:[outer]},NaN,1),false);
});
test('GPS denial and clearing pending requests never upload coordinates', () => {
    const nodes = new Map(); const get = id => { if (!nodes.has(id)) nodes.set(id, { addEventListener(e,fn) { this[e] = fn; } }); return nodes.get(id); };
    const ctx = {}; runInNewContext(readFileSync('wwwroot/js/resident-locate.js','utf8'),ctx);
    let success; const map = { removeLayer(){}, setView(){} };
    ctx.ResidentLocate.mount({ document:{getElementById:get}, navigator:{geolocation:{getCurrentPosition(ok,fail) { success = ok; fail(); }}},
        view:{map,getZones:()=>({features:[]})}, leaflet:{circleMarker(){assert.fail('Cancelled GPS must not render');}}, locator:context.ZoneLocator });
    get('resident-locate').click(); assert.match(get('resident-location-status').textContent,/GPS denied/);
    get('resident-location-clear').click(); success({coords:{latitude:2,longitude:1}});
    assert.equal(get('resident-location-status').textContent,'Location cleared.');
});
