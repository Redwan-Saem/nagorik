(() => {
    'use strict';
    const get = id => document.getElementById(id), input = get('backup-input');
    if (!input) return;
    let revision = 0, pending, lastResult = null;
    const example = {
        grid_available: [true, true, false, false, false, true, true, false, false, false],
        generator_capacity_kw: 10, fuel_budget_litres: 8.00,
        fuel_table: [{ load_kw: 4, litres_per_hour: 0.80 }, { load_kw: 7, litres_per_hour: 1.20 }, { load_kw: 10, litres_per_hour: 1.60 }],
        max_consecutive_dark: 2,
        shops: [
            { id: 'pharmacy', kw: 3, type: 'mandatory', value_per_slot: [5,5,5,5,5,5,5,5,5,5] },
            { id: 'tailor', kw: 4, type: 'normal', value_per_slot: [2,2,3,3,4,4,4,4,5,5] },
            { id: 'mobile', kw: 3, type: 'normal', value_per_slot: [1,1,2,2,2,3,4,6,8,8] },
            { id: 'photocopy', kw: 2, type: 'normal', value_per_slot: [3,3,5,5,4,3,2,2,1,1] },
            { id: 'tea_stall', kw: 1, type: 'normal', value_per_slot: [2,2,2,3,3,2,2,3,3,3] }
        ]
    };
    function control(label, type, value, attributes = {}) {
        const wrapper = node('label', label);
        const field = node(type === 'select' ? 'select' : 'input');
        if (type !== 'select') field.type = type;
        Object.assign(field, attributes);
        if (type === 'select') {
            for (const [key, name] of attributes.optionsList) {
                const option = node('option', name);
                option.value = key;
                field.append(option);
            }
        }
        field.value = value;
        wrapper.append(field);
        return wrapper;
    }
    const number = (label, value, min, max, step = 1) =>
        control(label, 'number', value, { min, max, step, required: true });
    const fields = row => [...row.querySelectorAll('input, select')];
    const rows = section => [...get('backup-' + section).children];
    const values = section => rows(section).map(row => fields(row).map(field =>
        field.type === 'number' ? field.valueAsNumber : field.value));
    const durations = () => values('slots').map(([minutes]) => minutes);
    const time = minutes => String(Math.floor(minutes / 60)).padStart(2, '0') + ':' + String(minutes % 60).padStart(2, '0');
    function startMinute() {
        const [hours = 0, minutes = 0] = get('backup-start').value.split(':').map(Number);
        return hours * 60 + minutes;
    }
    function slotLabels(minutes, start = startMinute()) {
        return minutes.map(duration => {
            const end = start + (duration || 0);
            const label = time(start) + '–' + time(end);
            start = end;
            return label;
        });
    }
    function button(text, action) {
        const el = node('button', text);
        el.type = 'button';
        el.addEventListener('click', action);
        return el;
    }
    function addBand(load = 1, rate = 0) {
        const row = node('div', undefined, 'backup-row');
        row.append(number('Up to load (kW)', load, 1, 1000000), number('Fuel (L/hour)', rate, 0, 1000000, 0.01),
            button('Remove band', () => { row.remove(); invalidate(); }));
        get('backup-bands').append(row);
    }
    function addSlot(minutes = 60, grid = false) {
        const row = node('div', undefined, 'backup-row');
        row.append(node('strong'), number('Duration (minutes)', minutes, 1, 1440),
            control('Supply', 'select', String(grid), { optionsList: [['true', 'Grid available'], ['false', 'Outage']] }),
            button('Remove slot', () => {
                if (rows('slots').length === 1) return;
                const index = rows('slots').indexOf(row);
                for (const shop of rows('shops')) shop.querySelector('.backup-priorities').children[index].remove();
                row.remove();
                timelineEdited();
            }));
        get('backup-slots').append(row);
    }
    function addPriority(shop, value) {
        shop.querySelector('.backup-priorities').append(number('Priority', value, 0, 1000000));
    }
    function addShop(shop = { id: '', kw: 1, type: 'normal', value_per_slot: [] }) {
        const row = node('section', undefined, 'backup-shop');
        const details = node('div', undefined, 'backup-row');
        details.append(control('Shop name', 'text', shop.id, { required: true, maxLength: 80 }),
            number('Demand (kW)', shop.kw, 1, 1000000),
            control('Requirement', 'select', shop.type, { optionsList: [['normal', 'Normal'], ['mandatory', 'Mandatory']] }),
            button('Remove shop', () => { row.remove(); updateTimeline(); invalidate(); }));
        row.append(details, node('div', undefined, 'backup-priorities'));
        durations().forEach((_, index) => addPriority(row, shop.value_per_slot[index] ?? 1));
        get('backup-shops').append(row);
    }
    function updateTimeline() {
        const labels = slotLabels(durations());
        rows('slots').forEach((row, index) => {
            row.firstElementChild.textContent = labels[index];
            row.lastElementChild.disabled = labels.length === 1;
        });
        for (const shop of rows('shops'))
            [...shop.querySelector('.backup-priorities').children].forEach((label, index) => {
                label.firstChild.textContent = 'Priority ' + labels[index];
            });
        get('backup-add-slot').disabled = labels.length >= 24;
        get('backup-add-shop').disabled = rows('shops').length >= 12;
    }
    function timelineEdited() {
        updateTimeline();
        invalidate();
        const source = get('backup-source');
        if (source) source.textContent = 'Timeline edited manually.';
    }
    function write(scenario) {
        get('backup-start').value = '10:00';
        get('backup-capacity').value = scenario.generator_capacity_kw;
        get('backup-budget').value = scenario.fuel_budget_litres;
        get('backup-dark-limit').value = scenario.max_consecutive_dark * 60;
        for (const section of ['bands', 'slots', 'shops']) get('backup-' + section).replaceChildren();
        scenario.fuel_table.forEach(band => addBand(band.load_kw, band.litres_per_hour));
        scenario.grid_available.forEach(grid => addSlot(60, grid));
        scenario.shops.forEach(addShop);
        updateTimeline();
    }
    function invalidate(message = 'Scenario changed. Generate a new roster before relying on it.') {
        revision++; pending?.abort(); pending = null;
        get('backup-run').disabled = false;
        lastResult = null; get('backup-results').hidden = true; get('backup-errors').replaceChildren();
        get('backup-status').textContent = message;
    }
    function read() {
        const minutes = durations();
        if (minutes.reduce((sum, value) => sum + value, startMinute()) > 1440) throw new Error('Shorten the timeline so it ends by midnight.');
        return {
            start_minute: startMinute(),
            slot_minutes: minutes,
            grid_available: values('slots').map(([, grid]) => grid === 'true'),
            generator_capacity_kw: get('backup-capacity').valueAsNumber,
            fuel_budget_litres: get('backup-budget').valueAsNumber,
            max_consecutive_dark_minutes: get('backup-dark-limit').valueAsNumber,
            fuel_table: values('bands').map(([load_kw, litres_per_hour]) => ({ load_kw, litres_per_hour })),
            shops: values('shops').map(([id, kw, type, ...value_per_slot]) => ({ id: id.trim(), kw, type, value_per_slot }))
        };
    }
    function node(tag, text, className) {
        const el = document.createElement(tag); if (text !== undefined) el.textContent = String(text);
        if (className) el.className = className; return el;
    }
    function render(result, scenario) {
        lastResult = result; get('backup-results').hidden = false;
        get('backup-print').disabled = result.status !== 'ok';
        get('backup-result-message').textContent = result.message || result.status;
        const summary = get('backup-summary'); summary.replaceChildren(node('p', 'Status: ' + result.status));
        const board = get('backup-board'), reasons = get('backup-reasons'); reasons.replaceChildren();
        board.hidden = result.status !== 'ok';
        if (result.status !== 'ok') return;
        if (!Array.isArray(result.roster) || !Array.isArray(scenario.shops) || !Array.isArray(scenario.grid_available)) throw new Error('Unexpected roster response.');
        summary.append(node('p', 'Total value: ' + result.value_total), node('p', 'Fuel: ' + Number(result.fuel_total_litres).toFixed(2) + ' / ' + Number(scenario.fuel_budget_litres).toFixed(2) + ' L'), node('p', result.optimality_proven ? 'Optimality proven' : 'Feasible; optimality not proven'));
        const head = board.querySelector('thead'), body = board.querySelector('tbody'), foot = board.querySelector('tfoot');
        head.replaceChildren(); body.replaceChildren(); foot.replaceChildren();
        const header = node('tr'); header.append(node('th', 'Shop / kW'));
        const labels = slotLabels(scenario.slot_minutes, scenario.start_minute);
        board.style.setProperty('--backup-columns', labels.length);
        labels.forEach(label => header.append(node('th', label)));
        header.append(node('th', 'Dark minutes')); head.append(header);
        const rows = new Map(result.roster.map(row => [row.slot, row]));
        for (const shop of scenario.shops) {
            const tr = node('tr'); tr.append(node('th', shop.id + ' / ' + shop.kw + ' kW' + (shop.type === 'mandatory' ? ' · Mandatory' : '')));
            for (let h = 0; h < labels.length; h++) {
                if (scenario.grid_available[h]) { tr.append(node('td', 'Grid', 'backup-grid')); continue; }
                const row = rows.get(h); if (!row) throw new Error('Missing outage row.');
                if (row.powered.includes(shop.id)) tr.append(node('td', 'On', 'backup-on'));
                else {
                    const dark = row.dark.find(x => x.id === shop.id); if (!dark) throw new Error('Missing dark-slot explanation.');
                    const td = node('td', 'Dark', 'backup-dark'); td.title = dark.reason; tr.append(td);
                    reasons.append(node('li', shop.id + ', ' + labels[h] + ': ' + dark.reason));
                }
            }
            tr.append(node('td', result.dark_minutes[shop.id])); body.append(tr);
        }
        for (const [label, value] of [
            ['Load / capacity (kW)', row => row.load_kw + ' / ' + scenario.generator_capacity_kw],
            ['Fuel this slot (L)', row => Number(row.fuel_litres).toFixed(2)],
            ['Running fuel / budget (L)', row => Number(row.cumulative_fuel_litres).toFixed(2) + ' / ' + Number(scenario.fuel_budget_litres).toFixed(2)]
        ]) {
            const tr = node('tr'); tr.append(node('th', label));
            for (let h = 0; h < labels.length; h++) tr.append(node('td', scenario.grid_available[h] ? 'Grid' : value(rows.get(h))));
            tr.append(node('td', '')); foot.append(tr);
        }
    }
    input.addEventListener('input', event => {
        if (event.target === get('backup-start') || get('backup-slots').contains(event.target)) timelineEdited();
        else invalidate();
    });
    get('backup-add-band').addEventListener('click', () => {
        addBand(get('backup-capacity').valueAsNumber || 1);
        invalidate();
    });
    get('backup-add-slot').addEventListener('click', () => {
        addSlot();
        rows('shops').forEach(shop => addPriority(shop, 1));
        timelineEdited();
    });
    get('backup-add-shop').addEventListener('click', () => { addShop(); updateTimeline(); invalidate(); });
    get('backup-example').addEventListener('click', () => {
        write(example);
        timelineEdited();
        get('backup-status').textContent = 'Worked example loaded. Generate to see the sample roster.';
    });
    input.addEventListener('submit', async event => {
        event.preventDefault();
        invalidate('Planning the whole day…'); const attempt = revision;
        const controller = new AbortController(); pending = controller; get('backup-run').disabled = true;
        const timeout = setTimeout(() => controller.abort(new Error('Planner request timed out. Check the server and retry.')), 12000);
        try {
            const scenario = read();
            const response = await fetch('/api/roster', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(scenario), signal: controller.signal, cache: 'no-store' });
            const result = await response.json(); if (attempt !== revision) return;
            if (!['ok', 'infeasible', 'invalid_input', 'not_solved', 'busy'].includes(result.status)) throw new Error('The server could not return a roster. Your input is kept; retry.');
            for (const [field, message] of Object.entries(result.errors || {})) get('backup-errors').append(node('li', field + ': ' + message));
            render(result, scenario); get('backup-status').textContent = result.message;
        } catch (error) {
            if (attempt === revision && error.name !== 'AbortError') { lastResult = null; get('backup-results').hidden = true; get('backup-status').textContent = error.message + ' Your input is kept.'; }
        } finally { clearTimeout(timeout); if (attempt === revision) { pending = null; get('backup-run').disabled = false; } }
    });
    get('backup-print').addEventListener('click', () => { if (lastResult?.status === 'ok') window.print(); else get('backup-status').textContent = 'Generate a feasible roster before printing.'; });
    const importer = get('backup-import');
    if (importer) {
        const parts = new Intl.DateTimeFormat('en', { timeZone: 'Asia/Dhaka', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
        const p = type => parts.find(x => x.type === type).value;
        get('backup-date').value = p('year') + '-' + p('month') + '-' + p('day');
        fetch('/api/services/zones', { cache: 'no-store' }).then(r => { if (!r.ok) throw new Error(); return r.json(); }).then(zones => {
            const select = get('backup-zone'); select.replaceChildren();
            for (const zone of zones) { const option = node('option', zone.name + ' (' + zone.code + ')'); option.value = zone.id; select.append(option); }
        }).catch(() => { get('backup-source').textContent = 'Zones could not load. Manual planning is still available. Reload to retry.'; });
        const cancelImport = () => { invalidate(); get('backup-source').textContent = 'Selection changed. Import this zone/date before generating a schedule-based plan.'; };
        get('backup-zone').addEventListener('change', cancelImport); get('backup-date').addEventListener('change', cancelImport);
        importer.addEventListener('click', async () => {
            invalidate('Loading the published plan…'); const attempt = revision;
            const controller = new AbortController(); pending = controller;
            const timeout = setTimeout(() => controller.abort(new Error('Published-plan request timed out. Retry or use reviewed manual hours.')), 8000);
            try {
                const previousSlots = durations().length;
                const response = await fetch('/api/backup-power/published-grid?zoneId=' + encodeURIComponent(get('backup-zone').value) + '&date=' + encodeURIComponent(get('backup-date').value), { credentials: 'same-origin', cache: 'no-store', signal: controller.signal });
                if (response.redirected || response.status === 401 || response.status === 403) throw new Error('Sign in as a resident or electricity official to import a plan.');
                const data = await response.json(); if (attempt !== revision) return;
                if (!response.ok) throw new Error(data.message || 'Published plan unavailable.');
                get('backup-start').value = '10:00';
                get('backup-slots').replaceChildren();
                data.grid_available.forEach(grid => addSlot(60, grid));
                for (const shop of rows('shops')) {
                    const priorities = shop.querySelector('.backup-priorities');
                    for (let i = previousSlots; i < data.grid_available.length; i++) addPriority(shop, 1);
                    while (priorities.children.length > data.grid_available.length) priorities.lastElementChild.remove();
                }
                updateTimeline();
                get('backup-source').textContent = data.source + ' · ' + data.zone_name + ' · ' + data.date + ' · Published ' + data.published_at_utc + '. ' + data.message;
                get('backup-status').textContent = 'Published outage hours imported. Review generator/shop inputs, then generate a new roster.';
            } catch (error) {
                if (attempt === revision && error.name !== 'AbortError') { get('backup-source').textContent = error.message; get('backup-status').textContent = 'No published hours imported; existing timeline was kept. Manual hours remain your responsibility.'; }
            } finally { clearTimeout(timeout); if (attempt === revision) pending = null; }
        });
    }
    write(example);
})();
