(() => {
    const labels = { SeatsAvailable: 'Seats available', Standing: 'Standing passengers', Full: 'Full' };
    function mount({ document = globalThis.document, fetcher = (...args) => globalThis.fetch(...args), pollMilliseconds = 30000 } = {}) {
        const get = id => document.getElementById(`occupancy-${id}`); if (!get('segment')) return;
        let segments = [], sequence = 0;
        const text = (tag, content) => { const node = document.createElement(tag); node.textContent = content; return node; };
        async function request(url, options = {}) {
            const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 8000);
            try {
                const response = await fetcher(url, { cache: 'no-store', credentials: 'same-origin', ...options, signal: controller.signal });
                const data = await response.json(); if (!response.ok) throw new Error(data.message || (response.status === 401 || response.status === 403 ? 'Sign in as a resident again.' : 'Request failed.'));
                return data;
            } finally { clearTimeout(timer); }
        }
        function render(data) {
            get('summary').replaceChildren();
            get('summary').appendChild(text('h2', data.level ? labels[data.level] || 'Unknown' : 'Unknown — no recent observations'));
            get('summary').appendChild(text('p', `${data.reports} recent reporters. Last observation: ${data.lastObservedAtUtc ? new Date(data.lastObservedAtUtc).toLocaleString('en-GB', { timeZone: 'Asia/Dhaka' }) + ' (Asia/Dhaka)' : 'None within 15 minutes'}.`));
            for (const [level, count] of Object.entries(data.distribution || {})) get('summary').appendChild(text('p', `${labels[level] || level}: ${count}`));
        }
        async function refresh() {
            const id = Number(get('segment').value), current = ++sequence;
            if (!id) { get('status').textContent = 'No enabled bus segments configured.'; return; }
            const segment = segments.find(s => s.id === id);
            get('source').textContent = `${segment?.isSample ? 'Demonstration bus segment — not operator verified. ' : ''}${segment?.source || ''}`;
            get('status').textContent = 'Checking recent observations…';
            try { const data = await request(`/api/transport/occupancy?linkId=${id}`); if (current === sequence) { render(data); get('status').textContent = 'Observations checked. Community reports are unverified.'; } }
            catch (error) { if (current === sequence) { get('summary').replaceChildren(); get('status').textContent = 'Cannot check observations. Refresh to retry. ' + (error.name === 'AbortError' ? 'Request timed out.' : error.message); } }
        }
        async function load() {
            try {
                const old = get('segment').value; segments = await request('/api/transport/occupancy/segments'); get('segment').replaceChildren();
                for (const segment of segments) { const option = text('option', `${segment.line}: ${segment.from} → ${segment.to}`); option.value = String(segment.id); get('segment').appendChild(option); }
                if (segments.some(s => String(s.id) === old)) get('segment').value = old;
                await refresh();
            } catch (error) { get('status').textContent = `Bus catalog unavailable. Refresh to retry. ${error.message}`; }
        }
        get('segment').addEventListener('change', () => { if (get('report-status')) get('report-status').textContent = ''; refresh(); });
        get('refresh').addEventListener('click', load);
        get('report')?.addEventListener('submit', async event => {
            event.preventDefault(); const id = Number(get('segment').value); if (!id) { get('report-status').textContent = 'Choose a bus segment first.'; return; }
            get('submit').disabled = true;
            try {
                await request('/api/transport/occupancy', { method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]').value }, body: JSON.stringify({ linkId: id, level: get('level').value }) });
                get('report-status').textContent = 'Observation saved. Thank you.'; await refresh();
            } catch (error) { get('report-status').textContent = `${error.name === 'AbortError' ? 'Request timed out; refresh to check whether it saved before retrying.' : error.message} Your selection is kept.`; }
            finally { get('submit').disabled = false; }
        });
        const timer = setInterval(() => { if (!document.hidden) refresh(); }, pollMilliseconds);
        return { ready: load(), refresh, dispose: () => clearInterval(timer) };
    }
    globalThis.BusOccupancy = { mount, labels };
    if (typeof module !== 'undefined') module.exports = globalThis.BusOccupancy;
    if (typeof document !== 'undefined') mount();
})();
