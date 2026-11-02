(() => {
    const colors = { Unknown: '#64748b', Low: '#15803d', Medium: '#d97706', High: '#dc2626', Severe: '#7e22ce' };
    function mount({ document = globalThis.document, leaflet = globalThis.L, fetcher = (...args) => globalThis.fetch(...args) } = {}) {
        const get = id => document.getElementById(id);
        if (!get('resident-risk-map')) return;
        if (!leaflet) { get('public-zones-status').textContent = 'Map assets unavailable. Refresh to retry.'; return; }
        const map = leaflet.map('resident-risk-map').setView([23.78,90.39], 11), layers = {}, pending = {};
        let zones = { type: 'FeatureCollection', features: [] };
        leaflet.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap contributors' })
            .addTo(map).on('tileerror', () => { get('public-tile-status').textContent = 'Background tiles unavailable. Data layers may still be inspected.'; });
        async function load(kind, endpoint) {
            if (pending[kind]) return; pending[kind] = true;
            const status = get(`public-${kind}-status`), toggle = get(`public-${kind}-toggle`);
            status.textContent = `Loading ${kind}…`;
            try {
                const response = await fetcher(`/api/public/map/${endpoint}`, { credentials: 'omit', cache: 'no-store', headers: { Accept: 'application/geo+json' } });
                if (!response.ok) throw new Error(`${kind} could not load. Refresh to retry.`);
                const data = await response.json();
                if (data.type !== 'FeatureCollection' || !Array.isArray(data.features)) throw new Error(`Invalid ${kind} response.`);
                const next = leaflet.geoJSON(data, {
                    style: feature => ({ color: colors[feature.properties?.riskLevel] || colors.Unknown, fillOpacity: .3 }),
                    pointToLayer: (feature, latlng) => leaflet.marker(latlng, { icon: leaflet.divIcon({ className: kind === 'reports' ? 'report-marker' : 'drainage-marker drainage-blocked',
                        html: kind === 'reports' ? 'R' : '!', iconSize: [26,26] }) }),
                    onEachFeature: (feature, marker) => {
                        const p = feature.properties || {}, popup = document.createElement('div');
                        const texts = kind === 'zones' ? [p.name || p.code, `Risk: ${p.riskLevel || 'Unknown'}`,
                            `Evaluated: ${p.lastComputedAtUtc || 'Not computed; displayed sample level is not a live assessment'}`,
                            `Boundary: ${p.boundaryIsSample === true ? 'Sample' : p.boundaryIsSample === false ? 'User-supplied' : 'Unknown provenance'}; ${p.boundarySource || 'Source unrecorded'}`,
                            `Metrics: ${p.inputIsSample === true ? 'Sample' : p.inputIsSample === false ? 'User-supplied' : 'Unknown provenance'}; ${p.inputSource || 'Source unrecorded'}`] :
                            kind === 'reports' ? [p.reference, `Category: ${p.category}`, `Status: ${p.status}`] : ['Blocked drainage point', `Zone #${p.zoneId}`];
                        for (const text of texts) { const node = document.createElement('p'); node.textContent = text || ''; popup.appendChild(node); }
                        marker.bindPopup(popup);
                        if (kind === 'zones') marker.on('click', () => { get('public-zone-details').textContent = texts.join('. '); });
                    }
                });
                if (layers[kind]) map.removeLayer(layers[kind]); layers[kind] = next; if (toggle.checked) next.addTo(map);
                if (kind === 'zones') {
                    zones = data;
                    const focused = Number(new URLSearchParams(globalThis.location?.search || '').get('zone'));
                    const feature = data.features.find(f => f.id === focused);
                    const bounds = feature ? leaflet.geoJSON(feature).getBounds() : next.getBounds();
                    if (data.features.length && bounds.isValid()) map.fitBounds(bounds, { maxZoom: 14, padding: [20,20] });
                }
                status.textContent = `${data.features.length} ${kind} loaded. ${data.omittedCount || data.invalidZoneCount || 0} omitted.`;
            } catch (error) { status.textContent = `${error.message} ${layers[kind] ? 'Previously loaded data remains visible.' : ''}`; }
            finally { pending[kind] = false; }
        }
        const definitions = [['zones','risk-zones'],['reports','reports'],['blockages','blockages']];
        for (const [kind] of definitions) get(`public-${kind}-toggle`).addEventListener('change', () => {
            if (layers[kind]) { if (get(`public-${kind}-toggle`).checked) layers[kind].addTo(map); else map.removeLayer(layers[kind]); }
        });
        const reload = () => Promise.all(definitions.map(([kind, endpoint]) => load(kind, endpoint)));
        get('public-map-refresh').addEventListener('click', reload);
        return { map, ready: reload(), reload, getZones: () => zones };
    }
    globalThis.ResidentRiskMap = { mount };
    if (typeof document !== 'undefined') globalThis.ResidentRiskMap.current = mount();
})();
