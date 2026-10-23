(() => {
    function mount({ document = globalThis.document, navigator = globalThis.navigator, view = globalThis.ResidentRiskMap?.current,
        leaflet = globalThis.L, locator = globalThis.ZoneLocator } = {}) {
        const button = document.getElementById('resident-locate'); if (!button || !view) return;
        const status = document.getElementById('resident-location-status'); let generation = 0, marker;
        button.addEventListener('click', () => {
            const attempt = ++generation;
            if (!navigator?.geolocation) { status.textContent = 'GPS is unavailable. Select a zone manually on the map.'; return; }
            button.disabled = true; status.textContent = 'Requesting your location…';
            const fail = () => { if (attempt !== generation) return; button.disabled = false; status.textContent = 'GPS denied, timed out or unavailable. Select a zone on the map instead.'; };
            try { navigator.geolocation.getCurrentPosition(position => {
                if (attempt !== generation) return; button.disabled = false;
                const { latitude, longitude } = position.coords;
                if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || Math.abs(latitude) > 90 || Math.abs(longitude) > 180) { fail(); return; }
                const zones = view.getZones().features;
                const matches = zones.filter(f => locator.contains(f.geometry,latitude,longitude));
                if (marker) view.map.removeLayer(marker);
                marker = leaflet.circleMarker([latitude,longitude], { radius: 8, color: '#2563eb' }).addTo(view.map);
                view.map.setView([latitude,longitude],14);
                status.textContent = matches.length ? matches.map(f => `${f.properties.name}: ${f.properties.riskLevel}; evaluated ${f.properties.lastComputedAtUtc || 'not computed'}`).join('. ') :
                    zones.length ? 'Your location is outside the available zone boundaries. This does not imply low risk.' : 'Zone data has not loaded. Refresh map data and try again.';
            }, fail, { enableHighAccuracy: true, timeout: 15000, maximumAge: 30000 }); } catch { fail(); }
        });
        document.getElementById('resident-location-clear').addEventListener('click', () => {
            generation++; button.disabled = false; if (marker) view.map.removeLayer(marker); marker = null; status.textContent = 'Location cleared.';
        });
    }
    globalThis.ResidentLocate = { mount };
    if (typeof document !== 'undefined') mount();
})();
