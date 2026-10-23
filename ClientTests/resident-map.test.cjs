const { test } = require('node:test'); const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs'); const { runInNewContext } = require('node:vm');
function setup(failed = false) {
    const context = { URLSearchParams }; runInNewContext(readFileSync('wwwroot/js/resident-map.js','utf8'), context);
    const nodes = new Map(), requests = [], popups = [];
    const get = id => { if (!nodes.has(id)) nodes.set(id, { checked: true, addEventListener(e, fn) { this[e] = fn; } }); return nodes.get(id); };
    const map = { removed: 0, setView() { return this; }, fitBounds() {}, removeLayer() { this.removed++; } };
    const leaflet = { map: () => map, tileLayer: () => ({ addTo() { return this; }, on() {} }),
        marker: () => ({}), divIcon: x => x, geoJSON(data, options = {}) {
            for (const f of data.features || []) options.onEachFeature?.(f, { bindPopup(p) { popups.push(p); }, on() {} });
            return { addTo() {}, getBounds: () => ({ isValid: () => true }) };
        } };
    const document = { getElementById: get, createElement: () => ({ children: [], appendChild(c) { this.children.push(c); }, set innerHTML(x) { assert.fail('Unsafe HTML'); } }) };
    const ui = context.ResidentRiskMap.mount({ document, leaflet, fetcher: async (url, options) => {
        requests.push({ url, options }); return { ok: !failed, json: async () => ({ type:'FeatureCollection', features:[{ id:1, properties:{ name:'<script>zone</script>', riskLevel:'High' } }] }) };
    } }); return { ui, get, requests, popups, map };
}
test('uses only anonymous public endpoints and safe popup text', async () => {
    const state = setup(); await state.ui.ready; assert.equal(state.requests.length, 3);
    assert.ok(state.requests.every(r => r.url.startsWith('/api/public/map/') && r.options.credentials === 'omit'));
    assert.equal(state.popups[0].children[0].textContent, '<script>zone</script>');
    state.get('public-zones-toggle').checked = false; state.get('public-zones-toggle').change(); assert.equal(state.map.removed, 1);
});
test('each failed layer has explicit retry guidance', async () => {
    const state = setup(true); await state.ui.ready;
    for (const kind of ['zones','reports','blockages']) assert.match(state.get(`public-${kind}-status`).textContent, /Refresh/);
});
