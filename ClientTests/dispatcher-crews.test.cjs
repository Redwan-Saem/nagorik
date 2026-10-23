const { test } = require('node:test'); const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs'); const { runInNewContext } = require('node:vm');
function setup(response) {
    const context = {}; runInNewContext(readFileSync('wwwroot/js/dispatcher-crews.js', 'utf8'), context);
    const nodes = new Map(), markers = [], groups = [];
    const get = id => { if (!nodes.has(id)) nodes.set(id, { checked: true, addEventListener(e, fn) { this[e] = fn; } }); return nodes.get(id); };
    const map = { removed: 0, removeLayer() { this.removed++; } };
    const leaflet = { marker(point, options) { return { options }; }, divIcon: x => x,
        geoJSON(data, options) { for (const feature of data.features) { const marker = options.pointToLayer(feature, [0,0]); marker.bindPopup = value => marker.popup = value; options.onEachFeature(feature, marker); markers.push(marker); }
            const group = { added: 0, addTo() { this.added++; } }; groups.push(group); return group; } };
    const document = { getElementById: get, createElement: () => ({ children: [], appendChild(x) { this.children.push(x); }, set innerHTML(x) { assert.fail('Unsafe HTML'); } }) };
    const ui = context.DispatcherCrews.mount({ map, document, leaflet, fetcher: async () => response }); return { get, ui, map, markers, groups };
}
test('shows busy/free/stale icons, safe names and independent toggle', async () => {
    const state = setup({ ok: true, json: async () => ({ type: 'FeatureCollection', features: [
        { properties: { name: '<script>name</script>', isBusy: true } }, { properties: {} }, { properties: { isStale: true } }
    ], omittedCount: 1 }) }); await state.ui.ready;
    assert.match(state.markers[0].options.icon.className, /busy/); assert.match(state.markers[1].options.icon.className, /free/);
    assert.match(state.markers[2].options.icon.className, /stale/); assert.equal(state.markers[0].popup.children[0].textContent, '<script>name</script>');
    state.get('map-crews-toggle').checked = false; state.get('map-crews-toggle').change(); assert.equal(state.map.removed, 1);
});
test('denied API access is explicit', async () => {
    const state = setup({ ok: false, status: 403 }); await state.ui.ready; assert.match(state.get('map-crews-status').textContent, /Dispatcher/);
});
