const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/dispatcher-map-layers.js'), 'utf8');
const drainage = { type: 'FeatureCollection', features: [{ type: 'Feature', id: 1, geometry: { type: 'Point', coordinates: [90.37, 23.74] }, properties: { status: 'Blocked', zoneCode: 'Z1', zoneName: '<script>zone</script>' } }] };
function setup(fetcher) {
    const context = {}; runInNewContext(script, context);
    const nodes = new Map(), layers = [], markers = [], requests = [];
    const element = () => ({ children: [], textContent: '', appendChild(child) { this.children.push(child); }, set innerHTML(_) { assert.fail('Use text nodes for untrusted data'); } });
    const get = id => { if (!nodes.has(id)) nodes.set(id, { ...element(), checked: true, addEventListener(name, handler) { this[name] = handler; } }); return nodes.get(id); };
    const map = { added: 0, removed: 0, closePopup() {}, removeLayer() { this.removed++; } };
    const leaflet = { divIcon: options => options, marker(latlng, options) { const marker = { latlng, options, bindPopup(popup) { this.popup = popup; } }; markers.push(marker); return marker; },
        geoJSON(data, options) { for (const feature of data.features) { const point = options.pointToLayer(feature, [feature.geometry.coordinates[1], feature.geometry.coordinates[0]]); options.onEachFeature(feature, point); }
            const layer = { addTo() { map.added++; return this; } }; layers.push(layer); return layer; } };
    const ui = context.DispatcherMapLayers.mount({ map, document: { getElementById: get, createElement: element }, leaflet,
        fetcher: async (url, options) => { requests.push({ url, options }); return fetcher(url, options); } });
    return { ui, get, map, layers, markers, requests, api: context.DispatcherMapLayers };
}
test('renders status icons and safe popup text using GeoJSON coordinate order', async () => {
    const state = setup(async () => ({ ok: true, json: async () => drainage })); await state.ui.ready;
    assert.equal(state.requests[0].url, '/api/map/drainage-points'); assert.equal(state.requests[0].options.credentials, 'same-origin');
    assert.equal(state.markers[0].options.icon.html, '!'); assert.match(state.markers[0].options.icon.className, /blocked/);
    assert.deepEqual(Array.from(state.markers[0].latlng), [23.74, 90.37]);
    assert.match(state.markers[0].popup.children[1].textContent, /<script>zone/);
    assert.equal(state.api.drainageSymbol('Clear'), '✓'); assert.equal(state.api.drainageSymbol('Unknown'), '?');
});
test('drainage toggle stays hidden on reload and can restore the layer', async () => {
    const state = setup(async () => ({ ok: true, json: async () => drainage })); await state.ui.ready;
    const toggle = state.get('map-drainage-toggle'); toggle.checked = false; toggle.change();
    await state.ui.reloadDrainage(); assert.equal(state.map.added, 1);
    toggle.checked = true; toggle.change(); assert.equal(state.map.added, 2);
});
for (const status of [401, 403, 500]) test(`drainage HTTP ${status} has explicit feedback`, async () => {
    const state = setup(async () => ({ ok: false, status })); await state.ui.ready;
    assert.equal(state.get('map-drainage-error').hidden, false); assert.equal(state.layers.length, 0);
});
test('empty drainage collection is explained', async () => {
    const state = setup(async () => ({ ok: true, json: async () => ({ type: 'FeatureCollection', features: [] }) })); await state.ui.ready;
    assert.match(state.get('map-drainage-status').textContent, /No drainage points/);
});
