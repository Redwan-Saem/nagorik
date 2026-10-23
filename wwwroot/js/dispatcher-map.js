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
            for (const [label, source, sample] of [['Boundary', properties.boundarySource, properties.boundaryIsSample], ['Metrics', properties.inputSource, properties.inputIsSample]]) {
                const node = document.createElement('p'); node.textContent = `${label}: ${sample === true ? 'Sample' : sample === false ? 'User-supplied' : 'Unknown provenance'}; ${source || 'Source unrecorded'}`; popup.appendChild(node);
            }
            const factors = document.createElement('p');
            const details = properties.factors, assessment = details?.assessment;
            const number = value => typeof value === 'number' && Number.isFinite(value) ? value.toFixed(2) : 'Unknown';
            factors.textContent = assessment
                ? `${assessment.explanation} Score: ${number(assessment.score)}; rainfall intensity: ${number(assessment.intensityMmPerHour)} mm/h; vulnerability: ${number(assessment.vulnerability)}. Elevation: ${number(details.elevationMeters)} m; drainage density: ${number(details.drainageDensityKmPerSqKm)} km/km²; history: ${number(details.historyScore)}. Thresholds M/H/S: ${number(details.thresholds?.mediumThreshold)}/${number(details.thresholds?.highThreshold)}/${number(details.thresholds?.severeThreshold)}.`
                : 'Contributing factors are unavailable until a risk evaluation runs.';
            popup.appendChild(factors);
            if (details?.past?.timestampUtc) {
                const source = document.createElement('p');
                source.textContent = `Rainfall window: ${details.past.timestampUtc}; fetched: ${details.past.fetchedAtUtc}. This is a saved evaluation; refresh to check for newer results.`;
                popup.appendChild(source);
            }
            zoneLayer.bindPopup(popup);
            zoneLayer.on('click', () => { get('map-zone-details').textContent = popup.children ? Array.from(popup.children).map(p => p.textContent).join(' ') : `${title.textContent}. ${risk.textContent}. ${time.textContent}. ${factors.textContent}`; });
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
        const pointLayers = globalThis.DispatcherMapLayers?.mount({ map, document, leaflet, fetcher });
        globalThis.DispatcherCrews?.mount({ map, document, leaflet, fetcher });
        return { map, ready, reload: load, pointLayers };
    }
    globalThis.DispatcherRiskMap = { colors, colorForRisk, mount };
    if (typeof document !== 'undefined') mount();
})();
