(() => {
    const colors = Object.freeze({ Unknown: '#64748b', Low: '#15803d', Medium: '#d97706', High: '#dc2626', Severe: '#7e22ce' });
    const colorForRisk = risk => colors[risk] || colors.Unknown;

    function mount({ document = globalThis.document, leaflet = globalThis.L,
        fetcher = (...args) => globalThis.fetch(...args) } = {}) {
        if (!document?.getElementById('dispatcher-risk-map')) return;
        const get = id => document.getElementById(id);
        const status = get('map-status'), error = get('map-error'), refresh = get('map-refresh');
        if (!leaflet) { error.hidden = false; error.textContent = 'Map assets could not load. Refresh the page.'; status.textContent = ''; return; }
        const map = leaflet.map('risk-zone-map').setView([23.78, 90.39], 11);
        const tiles = leaflet.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19, attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);
        tiles.on('tileerror', () => { get('map-tile-status').textContent = 'Background tiles are unavailable. Zone boundaries can still be inspected.'; });
        let layer, busy = false;
        const toggle = get('map-zone-toggle');
        function describeZone(feature, zoneLayer) {
            const properties = feature.properties || {};
            const popup = document.createElement('div'); popup.className = 'zone-popup';
            const title = document.createElement('h3'); title.textContent = `${properties.name || 'Unnamed zone'} (${properties.code || feature.id})`;
            popup.appendChild(title);
            const risk = document.createElement('p'); risk.textContent = `Risk: ${properties.riskLevel || 'Unknown'}`; popup.appendChild(risk);
            const time = document.createElement('p');
            const computed = properties.lastComputedAtUtc ? new Date(properties.lastComputedAtUtc) : null;
            time.textContent = computed && !Number.isNaN(computed.getTime())
                ? `Last computed: ${computed.toLocaleString()} (${computed.toISOString()})`
                : 'Not computed yet; any displayed sample level is not a live prediction.';
            popup.appendChild(time);
            zoneLayer.bindPopup(popup);
            zoneLayer.on('click', () => { get('map-zone-details').textContent = `${title.textContent}. ${risk.textContent}. ${time.textContent}`; });
        }
        async function load() {
            if (busy) return;
            busy = true; refresh.disabled = true; error.hidden = true; get('map-login').hidden = true;
            status.textContent = 'Loading risk zones…';
            try {
                const response = await fetcher('/api/map/risk-zones', {
                    credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/geo+json' }
                });
                if (response.status === 401) { get('map-login').hidden = false; throw new Error('Your session expired. Log in again.'); }
                if (response.status === 403) throw new Error('This map requires the Dispatcher role.');
                if (!response.ok) throw new Error('Risk zones could not be loaded. Choose Refresh zones to retry.');
                const data = await response.json();
                if (data?.type !== 'FeatureCollection' || !Array.isArray(data.features)) throw new Error('The server returned an invalid map response.');
                const next = leaflet.geoJSON(data, { onEachFeature: describeZone, style: feature => ({
                    color: colorForRisk(feature.properties.riskLevel), fillColor: colorForRisk(feature.properties.riskLevel),
                    weight: 2, fillOpacity: .35
                }) });
                if (layer) map.removeLayer(layer);
                layer = next;
                if (toggle.checked) layer.addTo(map);
                get('map-zone-details').textContent = 'Select a zone on the map to inspect its risk and last computation time.';
                if (data.features.length && layer.getBounds().isValid()) map.fitBounds(layer.getBounds(), { padding: [25, 25], maxZoom: 14 });
                status.textContent = data.features.length ? `${data.features.length} risk zones loaded.` : 'No risk zones are available yet.';
                if (data.invalidZoneCount > 0) status.textContent += ` ${data.invalidZoneCount} invalid zone boundaries were omitted.`;
            } catch (failure) {
                error.textContent = failure.message || 'Connection failed. Refresh zones to retry.'; error.hidden = false;
                status.textContent = layer ? 'Previously loaded zones remain visible; refresh failed.' : 'Zones have not loaded.';
            } finally { busy = false; refresh.disabled = false; }
        }
        refresh.addEventListener('click', load);
        toggle.addEventListener('change', () => {
            if (!layer) return;
            if (toggle.checked) layer.addTo(map);
            else { map.closePopup(); map.removeLayer(layer); }
        });
        const ready = load();
        return { map, ready, reload: load };
    }
    globalThis.DispatcherRiskMap = { colors, colorForRisk, mount };
    if (typeof document !== 'undefined') mount();
})();
