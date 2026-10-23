(function () {
    'use strict';
    const form = document.getElementById('schedule-generate');
    if (!form) return;
    const status = document.getElementById('schedule-status');
    const versions = document.getElementById('schedule-versions');
    const button = form.querySelector('button[type="submit"]');
    function message(error) { return error.message || 'Request failed. Check your connection and sign-in, then retry.'; }
    async function list() {
        try {
            const response = await fetch('/api/official/loadshedding', { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) throw new Error('Could not load recent schedules. Sign in as a utility official.');
            const rows = await response.json(); versions.replaceChildren();
            if (!rows.length) versions.textContent = 'No schedules generated yet.';
            rows.forEach(row => {
                const item = document.createElement('li'), link = document.createElement('a');
                link.href = '/official/loadshedding/' + encodeURIComponent(row.id);
                link.textContent = '#' + row.id + ' — ' + row.state + ' — ' + row.startDate + ' to ' + row.endDate;
                item.append(link); versions.append(item);
            });
        } catch (error) { versions.textContent = message(error); }
    }
    form.addEventListener('submit', async function (event) {
        event.preventDefault(); if (button.disabled) return;
        const zones = Array.from(form.querySelectorAll('.schedule-demand')).filter(row => row.querySelector('.zone-included').checked)
            .map(row => ({ zoneId: Number(row.dataset.zoneId), demandMw: Number(row.querySelector('.zone-demand').value) }));
        if (!zones.length || zones.length > 100 || zones.some(z => !Number.isFinite(z.demandMw) || z.demandMw <= 0)) {
            status.textContent = 'Select 1 to 100 zones and enter positive demand for every selected zone.'; return;
        }
        const capacity = Number(form.elements.AvailableCapacityMw.value);
        if (!Number.isFinite(capacity) || capacity <= 0) { status.textContent = 'Enter positive available capacity in MW.'; return; }
        button.disabled = true; status.textContent = 'Generating draft…';
        try {
            const response = await fetch('/api/official/loadshedding', {
                method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json',
                    RequestVerificationToken: form.querySelector('input[name="__RequestVerificationToken"]').value },
                body: JSON.stringify({ startDate: form.elements.StartDate.value, endDate: form.elements.EndDate.value,
                    availableCapacityMw: capacity, slotMinutes: Number(form.elements.SlotMinutes.value), zones })
            });
            const data = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(data.message || (data.errors ? Object.values(data.errors).flat().join(' ') : 'Generation failed. Check the period and your sign-in.'));
            window.location.assign('/official/loadshedding/' + encodeURIComponent(data.id));
        } catch (error) { status.textContent = message(error); button.disabled = false; }
    });
    list();
}());
