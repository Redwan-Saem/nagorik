(() => {
    const drainageSymbol = status => status === 'Blocked' ? '!' : status === 'Clear' ? '✓' : '?';
    const safePhotoUrl = value => typeof value === 'string' && /^\/(?:api\/report-photos\/[0-9a-f]{32}\.(?:jpg|png)|uploads\/reports\/[0-9a-f]{32}\.(?:jpg|jpeg|png))$/i.test(value);
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
        let reportGroup, reportsPending = false;
        const reportToggle = get('map-reports-toggle');
        function describeReport(feature, marker) {
            const properties = feature.properties || {};
            const popup = document.createElement('div'); popup.className = 'zone-popup map-report-popup';
            const heading = document.createElement('h3'); heading.textContent = properties.reference || `Report #${feature.id}`; popup.appendChild(heading);
            for (const value of [properties.title, `Location: ${properties.location || 'Not provided'}`, `Status: ${properties.status || 'Unknown'}`]) {
                const text = document.createElement('p'); text.textContent = value || ''; popup.appendChild(text);
            }
            const fallback = document.createElement('p'); fallback.className = 'map-photo-fallback'; fallback.textContent = 'No photo available.';
            if (safePhotoUrl(properties.photoUrl)) {
                const image = document.createElement('img'); image.className = 'map-report-photo'; image.alt = 'Photo of the reported issue'; image.loading = 'lazy';
                fallback.hidden = true;
                image.addEventListener('error', () => { image.hidden = true; fallback.hidden = false; fallback.textContent = 'Report photo is unavailable.'; });
                image.src = properties.photoUrl; popup.appendChild(image);
            }
            popup.appendChild(fallback);
            if (Number.isInteger(feature.id) && feature.id > 0) {
                const link = document.createElement('a'); link.textContent = 'Open dispatcher report'; link.href = `/Dispatch/Report/${feature.id}`; popup.appendChild(link);
            }
            marker.bindPopup(popup, { maxWidth: 240 });
        }
        async function loadReports() {
            if (!reportToggle || reportsPending) return;
            reportsPending = true;
            const reportStatus = get('map-reports-status'), reportError = get('map-reports-error');
            reportError.hidden = true; reportStatus.textContent = 'Loading reports…';
            try {
                if (!leaflet.markerClusterGroup) throw new Error('Report clustering assets could not load. Refresh the page.');
                const response = await fetcher('/api/map/reports', { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/geo+json' } });
                if (!response.ok) throw new Error(response.status === 401 ? 'Session expired. Log in again.' :
                    response.status === 403 ? 'Reports require the Dispatcher role.' : 'Reports could not load. Refresh the map to retry.');
                const data = await response.json();
                if (data?.type !== 'FeatureCollection' || !Array.isArray(data.features)) throw new Error('Invalid report map response.');
                const next = leaflet.markerClusterGroup({ showCoverageOnHover: false, zoomToBoundsOnClick: false, chunkedLoading: true,
                    iconCreateFunction: cluster => leaflet.divIcon({ className: 'report-cluster marker-cluster', html: `<span>${Number(cluster.getChildCount())}</span>`, iconSize: [36, 36] }) });
                next.on('clusterclick', event => { if (event.layer.getChildCount() <= 50) event.layer.spiderfy(); else event.layer.zoomToBounds(); });
                next.addLayer(leaflet.geoJSON(data, { pointToLayer: (feature, latlng) => leaflet.marker(latlng, {
                    title: `Report ${feature.properties?.reference || feature.id}`,
                    icon: leaflet.divIcon({ className: 'report-marker', html: 'R', iconSize: [26, 26], iconAnchor: [13, 13] })
                }), onEachFeature: describeReport }));
                if (reportGroup) map.removeLayer(reportGroup); reportGroup = next;
                if (reportToggle.checked) reportGroup.addTo(map);
                reportStatus.textContent = data.features.length ? `${data.features.length} located reports loaded. Select a cluster to expand it.` : 'No located reports are available.';
                if (data.omittedCount > 0) reportStatus.textContent += ` ${data.omittedCount} reports without usable coordinates or beyond the response limit were omitted.`;
            } catch (failure) {
                reportError.hidden = false; reportError.textContent = failure.message || 'Report connection failed.';
                reportStatus.textContent = reportGroup ? 'Previously loaded report markers remain visible; refresh failed.' : 'Report markers have not loaded.';
            } finally { reportsPending = false; }
        }
        reportToggle?.addEventListener('change', () => {
            if (!reportGroup) return;
            if (reportToggle.checked) reportGroup.addTo(map); else { map.closePopup(); map.removeLayer(reportGroup); }
        });
        get('map-refresh').addEventListener('click', loadReports);
        return { ready: Promise.all([loadDrainage(), loadReports()]), reloadDrainage: loadDrainage, reloadReports: loadReports };
    }
    globalThis.DispatcherMapLayers = { mount, drainageSymbol, safePhotoUrl };
})();
