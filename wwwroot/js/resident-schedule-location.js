(() => {
    'use strict';
    function mount({ document = globalThis.document, navigator = globalThis.navigator, locator = globalThis.ZoneLocator,
        getZones, onSelect } = {}) {
        const button = document.getElementById('schedule-locate');
        const status = document.getElementById('schedule-location-status');
        if (!button || !status) return { cancel() {} };
        let generation = 0;
        function cancel(message = 'Manual zone selection remains available.') { generation++; button.disabled = false; status.textContent = message; }
        button.addEventListener('click', async () => {
            const attempt = ++generation;
            if (!navigator?.geolocation) { status.textContent = 'GPS is unavailable. Choose your zone manually.'; return; }
            button.disabled = true; status.textContent = 'Loading area boundaries…';
            let zones;
            try {
                const result = await getZones(); zones = result.features;
                if (!Array.isArray(zones) || !zones.length || !locator?.contains) throw new Error('No boundaries');
            } catch {
                if (attempt === generation) { button.disabled = false; status.textContent = 'Area boundaries are unavailable. Choose your zone manually.'; }
                return;
            }
            if (attempt !== generation) return;
            status.textContent = 'Requesting your location…';
            const fail = () => {
                if (attempt !== generation) return;
                button.disabled = false; status.textContent = 'GPS was denied, timed out or is unavailable. Choose your zone manually.';
            };
            try {
                navigator.geolocation.getCurrentPosition(position => {
                    if (attempt !== generation) return;
                    const { latitude, longitude } = position.coords || {};
                    if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || Math.abs(latitude) > 90 || Math.abs(longitude) > 180) { fail(); return; }
                    button.disabled = false;
                    const matches = zones.filter(z => locator.contains(z.geometry, latitude, longitude));
                    if (!matches.length) { status.textContent = 'Your location is outside the available zone boundaries. Choose a zone manually; a nearest zone will not be guessed.'; return; }
                    if (matches.length > 1) { status.textContent = 'Your location matches multiple areas: ' + matches.map(z => z.properties.name).join(', ') + '. Choose the correct zone manually.'; return; }
                    status.textContent = 'Detected area: ' + matches[0].properties.name + '. You can change it manually.';
                    onSelect(matches[0].id);
                }, fail, { enableHighAccuracy: true, timeout: 15000, maximumAge: 30000 });
            } catch { fail(); }
        });
        document.getElementById('schedule-location-clear')?.addEventListener('click', () => cancel('Location cleared. Your selected zone is unchanged.'));
        return { cancel };
    }
    globalThis.ResidentScheduleLocation = { mount };
})();
