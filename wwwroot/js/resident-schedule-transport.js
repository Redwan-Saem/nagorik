(() => {
    'use strict';
    const prefix = 'nagorik-resident-schedule-v1:';
    const dayMilliseconds = 86400000;
    const dateOnly = value => typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) && Number.isFinite(Date.parse(value + 'T00:00:00Z'));
    const utcTime = value => typeof value === 'string' && /Z$/.test(value) && Number.isFinite(Date.parse(value));
    function validZones(rows) {
        return Array.isArray(rows) && rows.length <= 5000 && rows.every(z => z && Number.isInteger(z.id) && z.id > 0 &&
            typeof z.code === 'string' && z.code.length <= 40 && typeof z.name === 'string' && z.name.length <= 100);
    }
    function validSchedule(data, zoneId) {
        return data && data.zoneId === zoneId && typeof data.zoneName === 'string' && data.zoneName.length <= 100 &&
            typeof data.zoneCode === 'string' && data.zoneCode.length <= 40 && data.timeZone === 'Asia/Dhaka' && utcTime(data.fetchedAtUtc) &&
            (data.lastUpdatedAtUtc === null || utcTime(data.lastUpdatedAtUtc)) &&
            ['NotPublished','PartiallyPublished','NoOutages','Published'].includes(data.status) &&
            Array.isArray(data.days) && data.days.length === 7 && data.days.every((day, index) => day && dateOnly(day.date) &&
                (index === 0 || Date.parse(day.date) - Date.parse(data.days[index - 1].date) === dayMilliseconds) &&
                typeof day.isPublished === 'boolean' && (day.publishedAtUtc === null || utcTime(day.publishedAtUtc)) &&
                Array.isArray(day.outages) && day.outages.length <= 24 && (!day.isPublished ? day.outages.length === 0 : true) &&
                day.outages.every(s => s && s.date === day.date && Number.isInteger(s.startMinute) && Number.isInteger(s.endMinute) &&
                    s.startMinute >= 0 && s.startMinute < s.endMinute && s.endMinute <= 1440 && utcTime(s.startAtUtc) && utcTime(s.endAtUtc))) &&
            (data.nextOutage === null || data.nextOutage && data.days.some(day => day.outages.some(s => s.date === data.nextOutage.date && s.startMinute === data.nextOutage.startMinute && s.endMinute === data.nextOutage.endMinute)));
    }
    function create(options = {}) {
        const fetcher = options.fetch || ((...args) => globalThis.fetch(...args));
        const now = options.now || (() => Date.now());
        let storage = options.storage;
        if (storage === undefined) { try { storage = globalThis.localStorage; } catch { storage = null; } }
        const read = key => {
            try { const text = storage?.getItem(prefix + key); return text && text.length <= 500000 ? JSON.parse(text) : null; }
            catch { return null; }
        };
        const save = (key, value) => { try { storage?.setItem(prefix + key, JSON.stringify(value)); } catch { /* Online functionality does not depend on available storage. */ } };
        const remove = key => { try { storage?.removeItem(prefix + key); } catch { /* Storage may be disabled. */ } };
        function recent(value) { return value && Number.isFinite(value.savedAt) && now() - value.savedAt >= -60000 && now() - value.savedAt <= 7 * dayMilliseconds; }
        async function request(url) {
            const controller = typeof AbortController === 'function' ? new AbortController() : null;
            const timer = controller ? setTimeout(() => controller.abort(), options.timeoutMilliseconds || 10000) : null;
            try {
                const response = await fetcher(url, { credentials:'same-origin', cache:'no-store', signal:controller?.signal });
                const data = await response.json().catch(() => null);
                if (!response.ok) {
                    const error = new Error(typeof data?.message === 'string' ? data.message : 'Schedule request failed (' + response.status + ').');
                    error.status = response.status; throw error;
                }
                if (data === null) throw new Error('The schedule server returned an invalid response.');
                return data;
            } finally { if (timer !== null) clearTimeout(timer); }
        }
        const permitsFallback = error => !error.status || error.status >= 500 || error.status === 408 || error.status === 429;
        return {
            async zones() {
                try {
                    const data = await request('/api/resident/loadshedding/zones');
                    if (!validZones(data)) throw new Error('The zone list is invalid.');
                    const safe = data.map(z => ({ id:z.id, code:z.code, name:z.name }));
                    save('zones', { savedAt:now(), data:safe }); return { data:safe, offline:false };
                } catch (error) {
                    if (error.status === 401 || error.status === 403) remove('zones');
                    if (!permitsFallback(error)) throw error;
                    const cached = read('zones');
                    if (recent(cached) && validZones(cached.data)) return { data:cached.data, offline:true };
                    throw new Error(error.message + ' No recent cached zone list is available.');
                }
            },
            async read(zoneId) {
                if (!Number.isInteger(zoneId) || zoneId <= 0) throw new Error('Choose a valid zone.');
                const key = 'zone:' + zoneId;
                try {
                    const data = await request('/api/resident/loadshedding?zoneId=' + encodeURIComponent(zoneId));
                    if (!validSchedule(data, zoneId)) throw new Error('The schedule response is invalid.');
                    // Whitelist fields so caches never retain unrelated official or
                    // authentication data, even if a server contract expands later.
                    const cleanSlot = s => ({ date:s.date, startMinute:s.startMinute, endMinute:s.endMinute, startAtUtc:s.startAtUtc, endAtUtc:s.endAtUtc, isOngoing:s.isOngoing === true });
                    const days = data.days.map(day => ({ date:day.date, isToday:day.isToday === true, isPublished:day.isPublished,
                        publishedAtUtc:day.publishedAtUtc, outages:day.outages.map(cleanSlot) }));
                    const safe = { zoneId:data.zoneId, zoneCode:data.zoneCode, zoneName:data.zoneName, timeZone:data.timeZone,
                        today:data.today, fetchedAtUtc:data.fetchedAtUtc, lastUpdatedAtUtc:data.lastUpdatedAtUtc,
                        status:data.status, nextOutage:data.nextOutage ? days.flatMap(d => d.outages).find(s => s.date === data.nextOutage.date && s.startMinute === data.nextOutage.startMinute && s.endMinute === data.nextOutage.endMinute) : null, days };
                    const prior = read(key);
                    if (!prior?.data || Date.parse(prior.data.fetchedAtUtc) <= Date.parse(data.fetchedAtUtc)) save(key, { savedAt:now(), data:safe });
                    return { data:safe, offline:false };
                } catch (error) {
                    if (error.status === 401 || error.status === 403) { remove(key); remove('zones'); }
                    if (!permitsFallback(error)) throw error;
                    const cached = read(key);
                    const today = new Date(now() + 6 * 3600000).toISOString().slice(0,10);
                    if (recent(cached) && validSchedule(cached.data, zoneId) && cached.data.days[6].date >= today)
                        return { data:cached.data, offline:true };
                    throw new Error(error.message + ' No usable cached schedule is available for this zone.');
                }
            }
        };
    }
    globalThis.ResidentScheduleTransport = { create, pollMilliseconds:30000 };
})();
