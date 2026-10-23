(function () {
    'use strict';
    const root = document.getElementById('schedule-review'); if (!root) return;
    const id = root.dataset.scheduleId, endpoint = '/api/official/loadshedding/' + encodeURIComponent(id);
    const get = name => document.getElementById(name), status = get('review-status');
    const edit = get('schedule-edit'), publish = get('schedule-publish');
    let data, offset = 0, busy = false;
    const time = minute => String(Math.floor(minute / 60)).padStart(2, '0') + ':' + String(minute % 60).padStart(2, '0');
    function list(target, rows) {
        target.replaceChildren(); rows.forEach(text => { const li = document.createElement('li'); li.textContent = text; target.append(li); });
    }
    function zoneName(zoneId) { return data.demands.find(z => z.zoneId === zoneId)?.name || 'Zone #' + zoneId; }
    function option(value, text) { const item = document.createElement('option'); item.value = value; item.textContent = text; return item; }
    function newSlot() {
        edit.elements.SlotId.value = '0'; get('edit-remove').disabled = true;
        edit.elements.Date.value = get('review-date').value || data.startDate;
        get('edit-end-time').textContent = 'Ends ' + time(Number(edit.elements.StartMinute.value) + data.slotMinutes);
    }
    function renderSlots() {
        const rows = data.slots.filter(s => (!get('review-zone').value || s.zoneId === Number(get('review-zone').value)) &&
            (!get('review-date').value || s.date === get('review-date').value));
        if (offset >= rows.length) offset = Math.max(0, Math.floor((rows.length - 1) / 100) * 100);
        get('review-count').textContent = rows.length ? 'Showing ' + (offset + 1) + '–' + Math.min(offset + 100, rows.length) + ' of ' + rows.length + ' matching slots.' : 'No outages in this selection.';
        const body = get('review-slots'); body.replaceChildren();
        rows.slice(offset, offset + 100).forEach(slot => {
            const tr = document.createElement('tr');
            [zoneName(slot.zoneId), slot.date, time(slot.startMinute), time(slot.endMinute)].forEach(text => { const td = document.createElement('td'); td.textContent = text; tr.append(td); });
            const td = document.createElement('td');
            if (data.state === 'Draft') {
                const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Edit';
                button.addEventListener('click', () => {
                    if (busy) return;
                    edit.elements.SlotId.value = slot.id; edit.elements.ZoneId.value = slot.zoneId; edit.elements.Date.value = slot.date;
                    edit.elements.StartMinute.value = slot.startMinute; get('edit-remove').disabled = false;
                    get('edit-end-time').textContent = 'Ends ' + time(slot.endMinute); edit.scrollIntoView({ block:'center' });
                }); td.append(button);
            } else td.textContent = 'Published version is read-only';
            tr.append(td); body.append(tr);
        });
        get('review-previous').disabled = offset === 0; get('review-next').disabled = offset + 100 >= rows.length;
    }
    async function request(path, method, payload) {
        const response = await fetch(path, { method, credentials:'same-origin', cache:'no-store',
            headers: payload ? { 'Content-Type':'application/json', RequestVerificationToken: root.querySelector('input[name="__RequestVerificationToken"]').value } : {},
            body: payload ? JSON.stringify(payload) : undefined });
        const value = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(value.message || (value.errors ? Object.values(value.errors).flat().join(' ') : 'Request failed (' + response.status + '). Check sign-in and reload before retrying.'));
        return value;
    }
    async function audit() {
        try { list(get('review-audit'), (await request(endpoint + '/audit', 'GET')).map(a => a.atUtc + ' UTC · ' + a.action + ' · official #' + a.actorUserId + ' · ' + a.details)); }
        catch (error) { get('review-audit').textContent = error.message; }
    }
    function render() {
        get('review-summary').textContent = data.state + ' · Revision ' + data.revision + ' · ' + data.startDate + ' to ' + data.endDate +
            ' · Capacity ' + data.availableCapacityMw + ' MW · ' + data.slotMinutes + '-minute slots' + (data.publishedAtUtc ? ' · Published ' + data.publishedAtUtc : '');
        list(get('review-errors'), data.review.errors); list(get('review-warnings'), data.review.warnings);
        list(get('review-demands'), data.demands.map(z => z.name + ' (' + z.code + '): ' + z.demandMw + ' MW'));
        const selected = get('review-zone').value;
        get('review-zone').replaceChildren(option('', 'All included zones')); edit.elements.ZoneId.replaceChildren();
        data.demands.forEach(z => { get('review-zone').append(option(z.zoneId, z.name)); edit.elements.ZoneId.append(option(z.zoneId, z.name)); });
        get('review-zone').value = selected;
        get('review-date').min = edit.elements.Date.min = data.startDate; get('review-date').max = edit.elements.Date.max = data.endDate;
        if (!get('review-date').value) get('review-date').value = data.startDate;
        edit.elements.StartMinute.replaceChildren();
        for (let minute = 0; minute < 1440; minute += data.slotMinutes) edit.elements.StartMinute.append(option(minute, time(minute)));
        edit.hidden = publish.hidden = data.state !== 'Draft';
        publish.querySelector('button[type="submit"]').disabled = !data.review.isValid;
        publish.elements.Confirm.checked = publish.elements.AcknowledgeWarnings.checked = false;
        newSlot(); renderSlots(); audit();
    }
    async function load() {
        if (busy) return; busy = true; get('review-reload').disabled = true;
        try { data = await request(endpoint, 'GET'); render(); status.textContent = 'Latest schedule loaded.'; }
        catch (error) { status.textContent = error.message; }
        finally { busy = false; get('review-reload').disabled = false; }
    }
    async function mutate(path, method, payload) {
        if (busy || !data || data.state !== 'Draft') return;
        busy = true; root.querySelectorAll('form button').forEach(b => b.disabled = true); status.textContent = 'Saving…';
        try { data = await request(path, method, payload); render(); status.textContent = data.state === 'Published' ? 'Schedule published successfully.' : 'Draft saved. Review validation before publishing.'; }
        catch (error) { status.textContent = error.message; }
        finally { busy = false; root.querySelectorAll('form button').forEach(b => b.disabled = false);
            get('edit-remove').disabled = edit.elements.SlotId.value === '0'; publish.querySelector('button[type="submit"]').disabled = !data.review.isValid; }
    }
    function editPayload(remove) { return { expectedRevision:data.revision, slotId:Number(edit.elements.SlotId.value),
        zoneId:Number(edit.elements.ZoneId.value), date:edit.elements.Date.value, startMinute:Number(edit.elements.StartMinute.value),
        endMinute:Number(edit.elements.StartMinute.value) + data.slotMinutes, remove }; }
    edit.addEventListener('submit', e => { e.preventDefault(); mutate(endpoint + '/slots', 'PATCH', editPayload(false)); });
    get('edit-remove').addEventListener('click', () => mutate(endpoint + '/slots', 'PATCH', editPayload(true)));
    get('edit-new').addEventListener('click', newSlot);
    edit.elements.StartMinute.addEventListener('change', () => { get('edit-end-time').textContent = 'Ends ' + time(Number(edit.elements.StartMinute.value) + data.slotMinutes); });
    publish.addEventListener('submit', e => {
        e.preventDefault(); if (!publish.elements.Confirm.checked) { status.textContent = 'Confirm publication first.'; return; }
        mutate(endpoint + '/publish', 'POST', { expectedRevision:data.revision, confirm:true, acknowledgeWarnings:publish.elements.AcknowledgeWarnings.checked });
    });
    ['review-zone','review-date'].forEach(name => get(name).addEventListener('change', () => { offset = 0; if (data) renderSlots(); }));
    get('review-previous').addEventListener('click', () => { offset = Math.max(0, offset - 100); renderSlots(); });
    get('review-next').addEventListener('click', () => { offset += 100; renderSlots(); });
    get('review-reload').addEventListener('click', load); load();
}());
