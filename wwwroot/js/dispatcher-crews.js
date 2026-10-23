(() => {
    function mount({ map, document = globalThis.document, leaflet = globalThis.L, fetcher = (...args) => globalThis.fetch(...args) }) {
        const get = id => document.getElementById(id), toggle = get('map-crews-toggle');
        if (!toggle) return;
        let layer, pending = false;
        async function load() {
            if (pending) return; pending = true;
            const status = get('map-crews-status'); status.textContent = 'Loading crew locations…';
            try {
                const response = await fetcher('/api/map/crews', { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/geo+json' } });
                if (!response.ok) throw new Error(response.status === 401 ? 'Session expired. Log in again.' : response.status === 403 ? 'Crew locations require Dispatcher access.' : 'Crew locations could not load. Refresh to retry.');
                const data = await response.json();
                if (data.type !== 'FeatureCollection' || !Array.isArray(data.features)) throw new Error('Invalid crew map response.');
                const next = leaflet.geoJSON(data, {
                    pointToLayer: (feature, latlng) => {
                        const p = feature.properties || {}, state = p.isStale ? 'stale' : p.isBusy ? 'busy' : 'free';
                        return leaflet.marker(latlng, { title: `Crew ${p.name || feature.id}: ${state}`,
                            icon: leaflet.divIcon({ className: `crew-marker crew-${state}`, html: p.isStale ? '?' : p.isBusy ? 'B' : 'F', iconSize: [30, 30] }) });
                    },
                    onEachFeature: (feature, marker) => {
                        const p = feature.properties || {}, popup = document.createElement('div');
                        for (const text of [p.name || 'Crew', p.isBusy ? 'Busy: active task assigned' : 'Free: no active task',
                            `Last seen: ${p.lastSeenAtUtc || 'Unknown'}`, p.isStale ? 'Stale location — not a current position' : 'Recent location shared with a task update']) {
                            const node = document.createElement('p'); node.textContent = text; popup.appendChild(node);
                        }
                        marker.bindPopup(popup);
                    }
                });
                if (layer) map.removeLayer(layer); layer = next; if (toggle.checked) layer.addTo(map);
                status.textContent = `${data.features.length} crew locations loaded. ${data.omittedCount || 0} crews have no location or were omitted. Locations are not continuously tracked.`;
            } catch (error) { status.textContent = `${error.message} ${layer ? 'Previously loaded positions remain visible.' : ''}`; }
            finally { pending = false; }
        }
        toggle.addEventListener('change', () => { if (layer) { if (toggle.checked) layer.addTo(map); else map.removeLayer(layer); } });
        get('map-refresh').addEventListener('click', load);
        return { ready: load(), reload: load };
    }
    globalThis.DispatcherCrews = { mount };
})();
