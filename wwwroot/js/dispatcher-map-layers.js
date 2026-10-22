(() => {
    const drainageSymbol = status => status === 'Blocked' ? '!' : status === 'Clear' ? '✓' : '?';
    function mount({ map, document = globalThis.document, leaflet = globalThis.L,
        fetcher = (...args) => globalThis.fetch(...args) } = {}) {
        const get = id => document.getElementById(id);
        if (!get('map-drainage-toggle')) return;
        const toggle = get('map-drainage-toggle'), status = get('map-drainage-status'), error = get('map-drainage-error');
        let group, pending = false;
        function describe(feature, marker) {
            const properties = feature.properties || {};
            const popup = document.createElement('div'); popup.className = 'zone-popup';
            const title = document.createElement('h3'); title.textContent = `Drainage point #${feature.id}`; popup.appendChild(title);
            const zone = document.createElement('p'); zone.textContent = `Zone: ${properties.zoneName} (${properties.zoneCode})`; popup.appendChild(zone);
            const state = document.createElement('p'); state.textContent = `Status: ${properties.status}`; popup.appendChild(state);
            marker.bindPopup(popup);
        }
        async function loadDrainage() {
            if (pending) return;
            pending = true; error.hidden = true; status.textContent = 'Loading drainage points…';
            try {
                const response = await fetcher('/api/map/drainage-points', {
                    credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/geo+json' }
                });
                if (!response.ok) throw new Error(response.status === 401 ? 'Session expired. Log in again.' :
                    response.status === 403 ? 'Drainage points require the Dispatcher role.' : 'Drainage points could not load. Refresh the map to retry.');
                const data = await response.json();
                if (data?.type !== 'FeatureCollection' || !Array.isArray(data.features)) throw new Error('Invalid drainage map response.');
                const next = leaflet.geoJSON(data, {
                    pointToLayer(feature, latlng) {
                        const value = feature.properties?.status;
                        const state = ['Clear', 'Blocked'].includes(value) ? value.toLowerCase() : 'unknown';
                        return leaflet.marker(latlng, {
                            title: `Drainage point #${feature.id} — ${value || 'Unknown'}`,
                            icon: leaflet.divIcon({ className: `drainage-marker drainage-${state}`, html: drainageSymbol(value), iconSize: [28, 28], iconAnchor: [14, 14] })
                        });
                    }, onEachFeature: describe
                });
                if (group) map.removeLayer(group); group = next;
                if (toggle.checked) group.addTo(map);
                status.textContent = data.features.length ? `${data.features.length} drainage points loaded.` : 'No drainage points are available.';
                if (data.omittedCount > 0) status.textContent += ` ${data.omittedCount} points omitted by the response limit.`;
            } catch (failure) {
                error.hidden = false; error.textContent = failure.message || 'Drainage connection failed.';
                status.textContent = group ? 'Previously loaded drainage points remain visible; refresh failed.' : 'Drainage points have not loaded.';
            } finally { pending = false; }
        }
        toggle.addEventListener('change', () => {
            if (!group) return;
            if (toggle.checked) group.addTo(map); else { map.closePopup(); map.removeLayer(group); }
        });
        get('map-refresh').addEventListener('click', loadDrainage);
        return { ready: loadDrainage(), reloadDrainage: loadDrainage };
    }
    globalThis.DispatcherMapLayers = { mount, drainageSymbol };
})();
