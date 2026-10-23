(() => {
    const inside = point => Number.isFinite(point?.latitude) && Number.isFinite(point?.longitude) && point.latitude >= 23.60 && point.latitude <= 23.95 && point.longitude >= 90.20 && point.longitude <= 90.58;
    function create({ fetcher = (...args) => globalThis.fetch(...args), navigator = globalThis.navigator } = {}) {
        let places = [];
        return {
            async load(signal) {
                const response = await fetcher('/api/transport/places', { signal, credentials: 'omit', cache: 'no-store' });
                if (!response.ok) throw new Error('Landmarks could not load. Retry or use map coordinates.');
                const rows = await response.json();
                if (!Array.isArray(rows)) throw new Error('Invalid landmark response.');
                places = rows.filter(row => typeof row.name === 'string' && inside(row));
                return places;
            },
            resolve(text) { return places.find(p => p.name.toLocaleLowerCase() === String(text).trim().toLocaleLowerCase()) || null; },
            locate() {
                return new Promise((resolve, reject) => {
                    if (!navigator?.geolocation) { reject(new Error('GPS is unavailable. Choose a landmark or map pin.')); return; }
                    navigator.geolocation.getCurrentPosition(position => {
                        const point = { name: 'Current location', latitude: position.coords.latitude, longitude: position.coords.longitude };
                        if (!inside(point)) reject(new Error('GPS is outside the Dhaka service area. Choose another origin.')); else resolve(point);
                    }, error => reject(new Error(error.code === 1 ? 'Location permission denied. Choose a landmark or map pin.' : 'GPS is unavailable or timed out. Choose a landmark or map pin.')),
                    { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 });
                });
            }
        };
    }
    function mountMap({ document = globalThis.document, leaflet = globalThis.L, onPick } = {}) {
        if (!leaflet || !document.getElementById('transport-map')) return null;
        const map = leaflet.map('transport-map').setView([23.77, 90.39], 12), pins = {};
        let route;
        leaflet.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap contributors' }).addTo(map)
            .on('tileerror', () => { document.getElementById('transport-map-status').textContent = 'Background tiles unavailable. Pins and schematic routes still work.'; });
        map.on('click', event => {
            const point = { name: `Map pin ${event.latlng.lat.toFixed(5)}, ${event.latlng.lng.toFixed(5)}`, latitude: event.latlng.lat, longitude: event.latlng.lng };
            if (!inside(point)) { document.getElementById('transport-map-status').textContent = 'Choose a point inside the Dhaka service area.'; return; }
            onPick(document.getElementById('transport-pin-target').value, point);
        });
        return {
            setPoint(key, point) {
                if (pins[key]) map.removeLayer(pins[key]);
                if (point) {
                    const label = document.createElement('span'); label.textContent = `${key}: ${point.name}`;
                    pins[key] = leaflet.circleMarker([point.latitude, point.longitude], { radius: 8, color: key === 'origin' ? '#15803d' : '#dc2626' }).bindTooltip(label).addTo(map);
                }
            },
            draw(legs) {
                if (route) map.removeLayer(route);
                if (!legs?.length) return;
                route = leaflet.featureGroup(legs.map(leg => leaflet.polyline((leg.geometry || [leg.from, leg.to]).map(point => [point.latitude, point.longitude]), { color: leg.mode === 'Walk' ? '#64748b' : '#0a6b7c', dashArray: leg.mode === 'Walk' ? '5 6' : null }))).addTo(map);
                map.fitBounds(route.getBounds(), { padding: [20, 20], maxZoom: 15 });
            }
        };
    }
    globalThis.TransportLocations = { create, inside, mountMap };
    if (typeof module !== 'undefined') module.exports = globalThis.TransportLocations;
})();
