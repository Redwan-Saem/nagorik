const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/dispatcher-map.js'), 'utf8');
function setup(fetcher) {
    const context = {}; runInNewContext(script, context);
    const nodes = new Map(), layers = [], requests = [];
    const get = id => { if (!nodes.has(id)) nodes.set(id, { textContent: '', hidden: true, addEventListener(event, handler) { this[event] = handler; } }); return nodes.get(id); };
    const map = { setView() { return this; }, fitBounds() {}, removeLayer() {} };
    const leaflet = { map: () => map, tileLayer: () => ({ addTo() { return this; }, on() {} }),
        geoJSON(data, options) { const layer = { data, options, addTo() { return this; }, getBounds: () => ({ isValid: () => data.features.length > 0 }) }; layers.push(layer); return layer; } };
    const ui = context.DispatcherRiskMap.mount({ document: { getElementById: get }, leaflet,
        fetcher: async (url, options) => { requests.push({ url, options }); return fetcher(url, options); } });
    return { ui, get, layers, requests, api: context.DispatcherRiskMap };
}
const payload = { type: 'FeatureCollection', features: [{ type: 'Feature', properties: { riskLevel: 'High' } }] };

test('loads GeoJSON with cookies and colors the zones by stored risk', async () => {
    const state = setup(async () => ({ ok: true, json: async () => payload })); await state.ui.ready;
    assert.equal(state.requests[0].url, '/api/map/risk-zones');
    assert.equal(state.requests[0].options.credentials, 'same-origin');
    assert.equal(state.layers[0].options.style(payload.features[0]).color, '#dc2626');
    assert.equal(state.api.colorForRisk('unexpected'), '#64748b');
    assert.match(state.get('map-status').textContent, /1 risk zones loaded/);
    assert.equal(state.get('map-refresh').disabled, false);
});
for (const status of [401, 403, 500]) test(`explains HTTP ${status} and allows explicit retry`, async () => {
    const state = setup(async () => ({ ok: false, status })); await state.ui.ready;
    assert.equal(state.get('map-error').hidden, false); assert.equal(state.get('map-refresh').disabled, false);
    assert.equal(state.get('map-login').hidden, status !== 401);
    assert.equal(state.layers.length, 0);
});
test('empty collection is an explicit empty state', async () => {
    const state = setup(async () => ({ ok: true, json: async () => ({ type: 'FeatureCollection', features: [] }) }));
    await state.ui.ready; assert.match(state.get('map-status').textContent, /No risk zones/);
});
test('failed refresh preserves the previously loaded zones', async () => {
    let count = 0;
    const state = setup(async () => { if (count++) throw new Error('offline'); return { ok: true, json: async () => payload }; });
    await state.ui.ready; await state.ui.reload();
    assert.equal(state.layers.length, 1); assert.match(state.get('map-status').textContent, /Previously loaded/);
});
