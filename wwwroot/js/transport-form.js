(() => {
    function validate(origin, destination) {
        if (!origin || !destination) return 'Choose both origin and destination from suggestions, GPS or map pins.';
        if (!globalThis.TransportLocations.inside(origin) || !globalThis.TransportLocations.inside(destination)) return 'Both points must be inside the Dhaka service area.';
        const dy = (origin.latitude - destination.latitude) * 111000, dx = (origin.longitude - destination.longitude) * 101700;
        return Math.hypot(dx, dy) < 50 ? 'Origin and destination must be at least 50 metres apart.' : '';
    }
    function mount({ document = globalThis.document, fetcher = (...args) => globalThis.fetch(...args), provider = globalThis.TransportLocations.create(), location = globalThis.location } = {}) {
        const get = id => document.getElementById(`transport-${id}`);
        if (!get('form')) return;
        const points = { origin: null, destination: null }, generation = { origin: 0, destination: 0 };
        let active, serial = 0, latest;
        const announce = text => { get('status').textContent = text; };
        const clear = () => { serial++; active?.abort(); latest = null; get('options').replaceChildren(); get('detail').hidden = true; get('search').disabled = false; map?.draw([]); };
        const map = globalThis.TransportLocations.mountMap({ document, onPick: setPoint });
        function setPoint(key, point) {
            generation[key]++; clear(); points[key] = point; get(key).value = point.name; map?.setPoint(key, point);
            get('validation').textContent = '';
        }
        for (const key of ['origin', 'destination']) get(key).addEventListener('input', () => {
            generation[key]++; points[key] = null; map?.setPoint(key, null); clear(); announce('Location changed. Search again.');
        });
        async function load() {
            const controller = new AbortController(), timeout = setTimeout(() => controller.abort(), 8000);
            try {
                const places = await provider.load(controller.signal); get('places').replaceChildren();
                for (const place of places) { const option = document.createElement('option'); option.value = place.name; get('places').appendChild(option); }
                announce(places.length ? 'Landmarks ready. Choose locations and search.' : 'No landmarks configured. Try map pins; routes require a populated catalog.');
            } catch (error) { announce(error.name === 'AbortError' ? 'Landmarks timed out. Retry search or use map pins.' : error.message); }
            finally { clearTimeout(timeout); }
        }
        get('gps').addEventListener('click', async () => {
            const expected = ++generation.origin; get('gps').disabled = true; announce('Waiting for GPS permission and location…');
            try { const point = await provider.locate(); if (generation.origin === expected) { setPoint('origin', point); announce('GPS origin selected. Choose destination and search.'); } }
            catch (error) { if (generation.origin === expected) announce(error.message); }
            finally { get('gps').disabled = false; }
        });
        async function search() {
            for (const key of ['origin', 'destination']) {
                if (!points[key] || points[key].name !== get(key).value) points[key] = provider.resolve(get(key).value);
                map?.setPoint(key, points[key]);
            }
            const error = validate(points.origin, points.destination); get('validation').textContent = error;
            if (error) return;
            clear(); const sequence = serial, controller = new AbortController(); active = controller;
            const timeout = setTimeout(() => controller.abort(), 8000); get('search').disabled = true; announce('Searching catalog routes…');
            const query = new URLSearchParams({ originLat: points.origin.latitude, originLon: points.origin.longitude, destinationLat: points.destination.latitude, destinationLon: points.destination.longitude, sort: get('sort').value });
            try {
                const response = await fetcher(`/api/transport/routes?${query}`, { signal: controller.signal, credentials: 'omit', cache: 'no-store' });
                const result = await response.json(); if (!response.ok) throw new Error(result.message || 'Route search failed.');
                if (!Array.isArray(result.options)) throw new Error('Invalid route response.');
                if (sequence !== serial) return;
                latest = { ...result, query: query.toString(), origin: points.origin, destination: points.destination, selectedId: new URLSearchParams(location.search).get('id') };
                announce(`${result.message}${result.truncated ? ' Search limit reached; options may be incomplete.' : ''}`);
                document.dispatchEvent(new CustomEvent('transport-results', { detail: latest }));
            } catch (error) { if (sequence === serial) announce(`${error.name === 'AbortError' ? 'Search timed out.' : error.message} Your locations are kept. Retry search.`); }
            finally { clearTimeout(timeout); if (sequence === serial) get('search').disabled = false; }
        }
        // Editing while an old request is running must not leave Search disabled.
        for (const key of ['origin', 'destination']) get(key).addEventListener('input', () => { get('search').disabled = false; });
        get('form').addEventListener('submit', event => { event.preventDefault(); search(); });
        get('retry').addEventListener('click', async () => { await load(); await search(); });
        get('sort').addEventListener('change', search);
        function draw(option) { map?.draw(option?.legs || []); }
        const ready = load().then(() => {
            const params = new URLSearchParams(location.search);
            if (['originLat','originLon','destinationLat','destinationLon'].every(k => params.has(k))) {
                for (const key of ['origin','destination']) setPoint(key, { name: params.get(`${key}Name`) || key, latitude: Number(params.get(`${key}Lat`)), longitude: Number(params.get(`${key}Lon`)) });
                if (['fastest','cheapest','transfers'].includes(params.get('sort'))) get('sort').value = params.get('sort');
                return search();
            }
        });
        return { ready, search, draw, getLatest: () => latest };
    }
    globalThis.TransportForm = { mount, validate };
    if (typeof module !== 'undefined') module.exports = globalThis.TransportForm;
    if (typeof document !== 'undefined') globalThis.TransportForm.current = mount();
})();
