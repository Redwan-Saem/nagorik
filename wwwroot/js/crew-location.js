(() => {
    const capture = document.getElementById('crew-location-capture');
    if (!capture) return;
    const status = document.getElementById('crew-location-status');
    let current = null, generation = 0;
    globalThis.CrewTaskLocation = { current: () => current };
    capture.addEventListener('click', () => {
        const attempt = ++generation; current = null;
        if (!globalThis.navigator?.geolocation) { status.textContent = 'GPS unavailable. You can update the task without sharing location.'; return; }
        capture.disabled = true; status.textContent = 'Requesting location…';
        navigator.geolocation.getCurrentPosition(position => {
            if (attempt !== generation) return;
            capture.disabled = false;
            const { latitude, longitude } = position.coords;
            if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || Math.abs(latitude) > 90 || Math.abs(longitude) > 180) {
                status.textContent = 'Invalid GPS result. Continue without location.'; return;
            }
            current = { latitude, longitude };
            status.textContent = 'Location ready. It will be shared with dispatchers only when your task update succeeds.';
        }, () => {
            if (attempt !== generation) return;
            capture.disabled = false; status.textContent = 'GPS denied or unavailable. You can update the task without location.';
        }, { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 });
    });
    document.getElementById('crew-location-clear').addEventListener('click', () => {
        generation++; current = null; capture.disabled = false; status.textContent = 'Location sharing skipped.';
    });
})();
