const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/dispatcher-map-layers.js'), 'utf8');
const drainage = { type: 'FeatureCollection', features: [{ type: 'Feature', id: 1, geometry: { type: 'Point', coordinates: [90.37, 23.74] }, properties: { status: 'Blocked', zoneCode: 'Z1', zoneName: '<script>zone</script>' } }] };
function setup(fetcher, reportsEnabled = false) {
    const context = {}; runInNewContext(script, context);
    const nodes = new Map(), layers = [], markers = [], requests = [];
    const element = () => ({ children: [], dataset: {}, textContent: '', appendChild(child) { this.children.push(child); }, addEventListener(name, handler) { this[name] = handler; }, set innerHTML(_) { assert.fail('Use text nodes for untrusted data'); } });
    const get = id => { if (id === 'map-reports-toggle' && !reportsEnabled) return null; if (!nodes.has(id)) nodes.set(id, { ...element(), checked: true }); return nodes.get(id); };
    const map = { added: 0, removed: 0, closePopup() {}, removeLayer() { this.removed++; } };
    const clusters = [];
    const leaflet = { markerClusterGroup(options) { const cluster = { options, on(event, handler) { this[event] = handler; }, addLayer(layer) { this.layer = layer; }, addTo() { map.added++; return this; } }; clusters.push(cluster); return cluster; },
        divIcon: options => options, marker(latlng, options) { const marker = { latlng, options, bindPopup(popup) { this.popup = popup; } }; markers.push(marker); return marker; },
        geoJSON(data, options) { for (const feature of data.features) { const point = options.pointToLayer(feature, [feature.geometry.coordinates[1], feature.geometry.coordinates[0]]); options.onEachFeature(feature, point); }
            const layer = { addTo() { map.added++; return this; } }; layers.push(layer); return layer; } };
    const ui = context.DispatcherMapLayers.mount({ map, document: { getElementById: get, createElement: element, querySelector: () => ({ value: 'fixture-csrf-token' }) }, leaflet,
        fetcher: async (url, options) => { requests.push({ url, options }); return fetcher(url, options); } });
    return { ui, get, map, layers, markers, requests, clusters, api: context.DispatcherMapLayers };
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

const reports = { type: 'FeatureCollection', omittedCount: 2, features: [{ id: 42, geometry: { type: 'Point', coordinates: [90.37, 23.74] },
    properties: { reference: 'UA-000042', title: '<script>unsafe</script>', location: 'Campus', status: 'Pending', photoUrl: '/api/report-photos/11111111111141118111111111111111.png' } }] };
test('clusters report markers and renders local photos and report text safely', async () => {
    const state = setup(async url => ({ ok: true, json: async () => url.endsWith('/reports') ? reports : drainage }), true); await state.ui.ready;
    assert.equal(state.clusters.length, 1);
    const marker = state.markers.find(m => m.options.icon.className === 'report-marker');
    assert.equal(marker.popup.children[1].textContent, '<script>unsafe</script>');
    const image = marker.popup.children.find(n => n.className === 'map-report-photo');
    assert.equal(image.src, reports.features[0].properties.photoUrl); image.error(); assert.equal(image.hidden, true);
    assert.equal(marker.popup.children.at(-1).href, '/Dispatch/Report/42');
    assert.match(state.get('map-reports-status').textContent, /2 reports without usable coordinates/);
    assert.equal(state.clusters[0].options.iconCreateFunction({ getChildCount: () => 3 }).html, '<span>3</span>');
});
test('unsafe report photo sources are never accepted', async () => {
    const state = setup(async () => ({ ok: true, json: async () => drainage })); await state.ui.ready;
    for (const url of ['https://evil.invalid/photo.jpg', '//evil.invalid/x', 'javascript:evil()', '/uploads/reports/../../x.png']) assert.equal(state.api.safePhotoUrl(url), false);
});
test('reports can be hidden across refresh and clusters expand by count', async () => {
    const state = setup(async url => ({ ok: true, json: async () => url.endsWith('/reports') ? reports : drainage }), true); await state.ui.ready;
    let expanded = 0, zoomed = 0;
    state.clusters[0].clusterclick({ layer: { getChildCount: () => 3, spiderfy() { expanded++; } } });
    state.clusters[0].clusterclick({ layer: { getChildCount: () => 51, zoomToBounds() { zoomed++; } } });
    assert.equal(expanded, 1); assert.equal(zoomed, 1);
    const toggle = state.get('map-reports-toggle'); toggle.checked = false; toggle.change();
    const added = state.map.added; await state.ui.reloadReports(); assert.equal(state.map.added, added);
});
test('report loading failure does not break drainage layer', async () => {
    const state = setup(async url => url.endsWith('/reports') ? { ok: false, status: 500 } : { ok: true, json: async () => drainage }, true);
    await state.ui.ready; assert.equal(state.get('map-reports-error').hidden, false);
    assert.match(state.get('map-drainage-status').textContent, /1 drainage points/);
});

test('status editing sends CSRF token and expected state then reloads markers', async () => {
    const state = setup(async (url, options) => options.method === 'PATCH' ? { ok: true, json: async () => ({ id: 1, status: 'Clear' }) } : { ok: true, json: async () => drainage });
    await state.ui.ready;
    const clear = state.markers[0].popup.children.at(-1).children[0];
    await clear.click();
    const request = state.requests.find(r => r.options.method === 'PATCH');
    assert.equal(request.url, '/api/map/drainage-points/1/status');
    assert.equal(request.options.headers.RequestVerificationToken, 'fixture-csrf-token');
    assert.deepEqual(JSON.parse(request.options.body), { status: 'Clear', expectedStatus: 'Blocked' });
    assert.match(state.get('map-drainage-action').textContent, /marked Clear/);
    assert.equal(state.layers.length, 2);
});
test('double clicks cannot issue a second status write while pending', async () => {
    let finish;
    const state = setup(async (url, options) => options.method === 'PATCH' ? new Promise(resolve => { finish = resolve; }) : { ok: true, json: async () => drainage });
    await state.ui.ready;
    const clear = state.markers[0].popup.children.at(-1).children[0];
    const first = clear.click(); await clear.click();
    assert.equal(state.requests.filter(r => r.options.method === 'PATCH').length, 1);
    finish({ ok: true, json: async () => ({ id: 1, status: 'Clear' }) }); await first;
});
for (const status of [400, 401, 403, 409, 500]) test(`status HTTP ${status} requires refresh without blind retry`, async () => {
    const state = setup(async (url, options) => options.method === 'PATCH' ? { ok: false, status } : { ok: true, json: async () => drainage });
    await state.ui.ready; await state.markers[0].popup.children.at(-1).children[0].click();
    assert.equal(state.get('map-drainage-action-error').hidden, false);
    assert.equal(state.requests.filter(r => r.options.method === 'PATCH').length, 1);
    assert.equal(state.layers.length, 1);
});
