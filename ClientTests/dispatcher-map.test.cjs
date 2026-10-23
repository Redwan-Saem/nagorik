const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/dispatcher-map.js'), 'utf8');
function setup(fetcher) {
    const context = {}; runInNewContext(script, context);
    const nodes = new Map(), layers = [], requests = [];
    const element = () => ({ textContent: '', children: [], appendChild(child) { this.children.push(child); },
        set innerHTML(value) { assert.fail('Untrusted zone strings must not become HTML'); } });
    const get = id => { if (!nodes.has(id)) nodes.set(id, { ...element(), checked: true, hidden: true, addEventListener(event, handler) { this[event] = handler; } }); return nodes.get(id); };
    const map = { added: 0, removed: 0, closed: 0, setView() { return this; }, fitBounds() {}, removeLayer() { this.removed++; }, closePopup() { this.closed++; } };
    const zoneLayers = [];
    const leaflet = { map: () => map, tileLayer: () => ({ addTo() { return this; }, on() {} }),
        geoJSON(data, options) {
            for (const feature of data.features) { const zone = { bindPopup(value) { this.popup = value; }, on(event, handler) { this[event] = handler; } }; zoneLayers.push(zone); options.onEachFeature?.(feature, zone); }
            const layer = { data, options, addTo() { map.added++; return this; }, getBounds: () => ({ isValid: () => data.features.length > 0 }) }; layers.push(layer); return layer;
        } };
    const ui = context.DispatcherRiskMap.mount({ document: { getElementById: get, createElement: element }, leaflet,
        fetcher: async (url, options) => { requests.push({ url, options }); return fetcher(url, options); } });
    return { ui, get, layers, requests, zoneLayers, map, api: context.DispatcherRiskMap };
}
const payload = { type: 'FeatureCollection', features: [{ type: 'Feature', properties: { riskLevel: 'High' } }] };

test('computed factors are visible as safe text in popup and accessible details', async () => {
    const data = { ...payload, features: [{ properties: { riskLevel: 'High', factors: {
        assessment: { explanation: '<script>bad()</script>', score: 20, intensityMmPerHour: 10, vulnerability: 2 },
        elevationMeters: 5, drainageDensityKmPerSqKm: 3, historyScore: .1,
        thresholds: { mediumThreshold: 5, highThreshold: 15, severeThreshold: 30 }
    } } }] };
    const state = setup(async () => ({ ok: true, json: async () => data })); await state.ui.ready;
    assert.match(state.zoneLayers[0].popup.children[3].textContent, /Score: 20.00/);
    assert.match(state.zoneLayers[0].popup.children[3].textContent, /<script>/);
    state.zoneLayers[0].click(); assert.match(state.get('map-zone-details').textContent, /rainfall intensity: 10.00/);
});

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

test('layer toggle hides and restores zones and stays hidden across refresh', async () => {
    const state = setup(async () => ({ ok: true, json: async () => payload })); await state.ui.ready;
    const toggle = state.get('map-zone-toggle'); toggle.checked = false; toggle.change();
    assert.equal(state.map.removed, 1); assert.equal(state.map.closed, 1);
    await state.ui.reload(); assert.equal(state.map.added, 1);
    toggle.checked = true; toggle.change(); assert.equal(state.map.added, 2);
});

test('popup treats names as text, explains missing computation and updates accessible details', async () => {
    const data = { type: 'FeatureCollection', features: [{ id: 1, properties: { name: '<img src=x onerror=evil()>', code: 'Z1', riskLevel: 'High', lastComputedAtUtc: null } }] };
    const state = setup(async () => ({ ok: true, json: async () => data })); await state.ui.ready;
    assert.match(state.zoneLayers[0].popup.children[0].textContent, /<img src=x/);
    assert.match(state.zoneLayers[0].popup.children[2].textContent, /Not computed yet/);
    state.zoneLayers[0].click();
    assert.match(state.get('map-zone-details').textContent, /Risk: High/);
});

test('popup displays actual computation time and refresh reports skipped geometry', async () => {
    const data = { type: 'FeatureCollection', invalidZoneCount: 1, features: [{ properties: { name: 'Zone', code: 'Z1', riskLevel: 'Low', lastComputedAtUtc: '2026-10-22T10:00:00Z' } }] };
    const state = setup(async () => ({ ok: true, json: async () => data })); await state.ui.ready;
    assert.match(state.zoneLayers[0].popup.children[2].textContent, /2026-10-22T10:00:00.000Z/);
    assert.match(state.get('map-status').textContent, /1 invalid zone/);
});
