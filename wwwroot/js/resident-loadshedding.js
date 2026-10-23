(() => {
    'use strict';
    const root = document.getElementById('resident-schedule'); if (!root) return;
    const get = id => document.getElementById(id), select = get('schedule-zone'), status = get('resident-schedule-status');
    const time = minute => String(Math.floor(minute / 60)).padStart(2, '0') + ':' + String(minute % 60).padStart(2, '0');
    const dateLabel = date => new Date(date + 'T12:00:00+06:00').toLocaleDateString(undefined,
        { timeZone:'Asia/Dhaka', weekday:'long', day:'numeric', month:'short', year:'numeric' });
    const stamp = value => value ? new Date(value).toLocaleString(undefined, { timeZone:'Asia/Dhaka' }) + ' Asia/Dhaka' : 'Not published';
    async function json(url) {
        const response = await fetch(url, { credentials:'same-origin', cache:'no-store' });
        const data = await response.json().catch(() => null);
        if (!response.ok) { const error = new Error(data?.message || 'Could not load schedules (' + response.status + ').'); error.status = response.status; throw error; }
        if (!data) throw new Error('The schedule server returned an invalid response.');
        return data;
    }
    const transport = globalThis.ResidentScheduleTransport?.create() || {
        zones: async () => ({ data:await json('/api/resident/loadshedding/zones'), offline:false }),
        read: async zoneId => ({ data:await json('/api/resident/loadshedding?zoneId=' + encodeURIComponent(zoneId)), offline:false })
    };
    let generation = 0, pending = 0, locationControl, zonesReady = false;
    function clearPlan() {
        get('schedule-days').replaceChildren(); get('schedule-zone-name').textContent = select.selectedOptions[0]?.textContent || 'Choose a zone';
        get('schedule-next-outage').textContent = 'Loading the selected zone…'; get('schedule-publication-summary').textContent = '';
        get('schedule-last-updated').textContent = ''; get('schedule-cache-notice').hidden = true;
    }
    function render(result) {
        const data = result.data; get('schedule-zone-name').textContent = data.zoneName + ' (' + data.zoneCode + ')';
        get('schedule-publication-summary').textContent = data.status === 'NotPublished' ? 'Schedule not yet published.' :
            data.status === 'NoOutages' ? 'No loadshedding scheduled for your zone.' : data.status === 'PartiallyPublished' ?
                'The plan is published for some days. Unpublished days are marked below.' : 'Published seven-day plan.';
        get('schedule-last-updated').textContent = 'Last updated: ' + stamp(data.lastUpdatedAtUtc) + ' · Last downloaded: ' + stamp(data.fetchedAtUtc);
        const notice = get('schedule-cache-notice'); notice.hidden = !result.offline;
        notice.textContent = result.offline ? 'Offline or unable to reach the server. Showing the last downloaded plan for ' +
            data.days[0].date + ' to ' + data.days[6].date + '. New publications cannot be checked until connected.' : '';
        const parts = new Intl.DateTimeFormat('en', { timeZone:'Asia/Dhaka', year:'numeric', month:'2-digit', day:'2-digit' }).formatToParts(new Date());
        const part = type => parts.find(p => p.type === type).value;
        const today = part('year') + '-' + part('month') + '-' + part('day');
        const list = get('schedule-days'); list.replaceChildren();
        data.days.forEach(day => {
            const item = document.createElement('li'); item.className = 'schedule-day' + (day.date === today ? ' schedule-today' : '');
            if (day.date === today) item.setAttribute('aria-current', 'date');
            const heading = document.createElement('h3'); heading.textContent = dateLabel(day.date) + (day.date === today ? ' — Today' : ''); item.append(heading);
            const text = document.createElement('p'); text.textContent = !day.isPublished ? 'Schedule not yet published.' :
                day.outages.length ? 'Published outage times:' : 'No loadshedding scheduled for your zone.'; item.append(text);
            if (day.isPublished && day.outages.length) {
                const slots = document.createElement('ul'); day.outages.forEach(slot => { const li = document.createElement('li'); li.textContent = time(slot.startMinute) + '–' + time(slot.endMinute) + ' Asia/Dhaka'; slots.append(li); }); item.append(slots);
            }
            if (day.isPublished) { const updated = document.createElement('p'); updated.textContent = 'Published: ' + stamp(day.publishedAtUtc); item.append(updated); }
            list.append(item);
        });
        const now = Date.now();
        const next = result.offline ? data.days.flatMap(d => d.outages).filter(s => Date.parse(s.endAtUtc) > now).sort((a,b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc))[0] : data.nextOutage;
        get('schedule-next-outage').textContent = next ? (Date.parse(next.startAtUtc) <= now ? 'Ongoing planned outage: ' : 'Next planned outage: ') +
            dateLabel(next.date) + ', ' + time(next.startMinute) + '–' + time(next.endMinute) + ' Asia/Dhaka' :
            data.status === 'NotPublished' ? 'Schedule not yet published.' : 'No further outage in the published days shown. Unpublished days remain unknown.';
    }
    async function load(automatic = false) {
        if (!zonesReady || !select.value || (automatic && pending)) return;
        const attempt = ++generation, zoneId = Number(select.value); pending++;
        get('schedule-refresh').disabled = true; get('schedule-sign-in').hidden = true; status.textContent = 'Checking the published schedule…';
        try {
            const result = await transport.read(zoneId);
            if (attempt !== generation || Number(select.value) !== zoneId) return;
            render(result); status.textContent = result.offline ? 'Cached schedule loaded. Check the download time before relying on it.' : 'Published schedule checked successfully.';
        } catch (error) {
            if (attempt !== generation) return;
            if (error.status === 401 || error.status === 403) { clearPlan(); get('schedule-next-outage').textContent = 'Sign in as a resident to view schedules.'; get('schedule-sign-in').hidden = false; }
            else if (error.status === 400 || error.status === 404) { clearPlan(); get('schedule-next-outage').textContent = 'Choose a currently available zone.'; }
            status.textContent = error.message + ' Your zone selection is kept. Use Refresh schedule to retry.';
        } finally { pending--; if (attempt === generation) get('schedule-refresh').disabled = false; }
    }
    function choose(zoneId, fromGps = false) {
        const option = Array.from(select.options).find(o => Number(o.value) === Number(zoneId));
        if (!option) { get('schedule-location-status').textContent = 'This area is not in the available zone list. Choose manually.'; return; }
        if (!fromGps) locationControl?.cancel(); select.value = option.value;
        try { localStorage.setItem('nagorik-schedule-selected-zone', select.value); } catch { /* Selection still works when storage is disabled. */ }
        clearPlan(); load();
    }
    select.addEventListener('change', () => choose(select.value));
    get('schedule-refresh').addEventListener('click', () => zonesReady ? load() : initialize());
    async function initialize() {
        try {
            const result = await transport.zones(); select.replaceChildren();
            if (!Array.isArray(result.data) || !result.data.length) { status.textContent = 'No zones are configured yet. Ask the administrator to add reviewed zones.'; get('schedule-refresh').disabled = false; return; }
            result.data.forEach(zone => { const option = document.createElement('option'); option.value = zone.id; option.textContent = zone.name + ' (' + zone.code + ')'; select.append(option); });
            zonesReady = true; select.disabled = false; get('schedule-locate').disabled = false;
            let selected = new URLSearchParams(window.location.search).get('zone');
            try { selected ||= localStorage.getItem('nagorik-schedule-selected-zone'); } catch { /* Optional preference. */ }
            if (!Array.from(select.options).some(o => o.value === selected)) selected = select.options[0].value;
            locationControl = globalThis.ResidentScheduleLocation.mount({ getZones:() => json('/api/public/map/risk-zones'), onSelect:id => choose(id, true) });
            choose(selected);
        } catch (error) {
            status.textContent = error.message + ' Use Refresh schedule to retry.'; get('schedule-refresh').disabled = false;
            if (error.status === 401 || error.status === 403) get('schedule-sign-in').hidden = false;
        }
    }
    window.addEventListener('online', () => zonesReady ? load(true) : initialize());
    const interval = globalThis.ResidentScheduleTransport?.pollMilliseconds || 0;
    if (interval) {
        window.setInterval(() => { if (!document.hidden) load(true); }, interval);
        document.addEventListener('visibilitychange', () => { if (!document.hidden) load(true); });
    }
    if (navigator.serviceWorker) navigator.serviceWorker.register('/sw.js', { updateViaCache:'none' }).catch(() => {
        // The open page can still use its data cache without offline navigation.
    });
    initialize();
})();
