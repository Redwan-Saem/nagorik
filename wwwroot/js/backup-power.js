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
    const write = value => { input.value = JSON.stringify(value, null, 2); };
    function invalidate(message = 'Scenario changed. Generate a new roster before relying on it.') {
        revision++; pending?.abort(); pending = null;
        get('backup-run').disabled = false;
        lastResult = null; get('backup-results').hidden = true; get('backup-errors').replaceChildren();
        get('backup-status').textContent = message;
    }
    function read() {
        if (new Blob([input.value]).size > 128 * 1024) throw new Error('Input is larger than 128 KB.');
        const value = JSON.parse(input.value);
        if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Supply a JSON object.');
        return value;
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
        for (let h = 0; h < 10; h++) header.append(node('th', (h + 10) + ':00'));
        header.append(node('th', 'Dark hours')); head.append(header);
        const rows = new Map(result.roster.map(row => [row.slot, row]));
        for (const shop of scenario.shops) {
            const tr = node('tr'); tr.append(node('th', shop.id + ' / ' + shop.kw + ' kW' + (shop.type === 'mandatory' ? ' · Mandatory' : '')));
            for (let h = 0; h < 10; h++) {
                if (scenario.grid_available[h]) { tr.append(node('td', 'Grid', 'backup-grid')); continue; }
                const row = rows.get(h); if (!row) throw new Error('Missing outage row.');
                if (row.powered.includes(shop.id)) tr.append(node('td', 'On', 'backup-on'));
                else {
                    const dark = row.dark.find(x => x.id === shop.id); if (!dark) throw new Error('Missing dark-slot explanation.');
                    const td = node('td', 'Dark', 'backup-dark'); td.title = dark.reason; tr.append(td);
                    reasons.append(node('li', shop.id + ', ' + (h + 10) + ':00: ' + dark.reason));
                }
            }
            tr.append(node('td', result.dark_hours[shop.id])); body.append(tr);
        }
        for (const [label, value] of [
            ['Load / capacity (kW)', row => row.load_kw + ' / ' + scenario.generator_capacity_kw],
            ['Fuel this hour (L)', row => Number(row.fuel_litres).toFixed(2)],
            ['Running fuel / budget (L)', row => Number(row.cumulative_fuel_litres).toFixed(2) + ' / ' + Number(scenario.fuel_budget_litres).toFixed(2)]
        ]) {
            const tr = node('tr'); tr.append(node('th', label));
            for (let h = 0; h < 10; h++) tr.append(node('td', scenario.grid_available[h] ? 'Grid' : value(rows.get(h))));
            tr.append(node('td', '')); foot.append(tr);
        }
    }
    input.addEventListener('input', () => {
        invalidate(); const source = get('backup-source'); if (source) source.textContent = 'Input edited. Import again to use the current published schedule snapshot.';
    });
    get('backup-example').addEventListener('click', () => {
        invalidate('Worked example loaded: sample data, not an operational schedule. Generate to solve it.'); write(example);
        const source = get('backup-source'); if (source) source.textContent = 'Worked-example outage hours; no published plan imported.';
    });
    get('backup-file').addEventListener('change', async event => {
        const file = event.target.files[0]; if (!file) return;
        invalidate('Reading scenario…'); const attempt = revision;
        try {
            if (file.size > 128 * 1024) throw new Error('File is larger than 128 KB.');
            const value = JSON.parse(await file.text());
            if (attempt !== revision) return;
            if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Supply a JSON object.');
            write(value); get('backup-status').textContent = 'File loaded. Review the inputs, then generate.';
            const source = get('backup-source'); if (source) source.textContent = 'Imported file; published-source metadata has not been verified.';
        } catch (error) { if (attempt === revision) get('backup-status').textContent = error.message; }
        event.target.value = '';
    });
    get('backup-run').addEventListener('click', async () => {
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
    function download(value, name) {
        const url = URL.createObjectURL(new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' }));
        const link = node('a'); link.href = url; link.download = name; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
    get('backup-input-download').addEventListener('click', () => { try { download(read(), 'backup-power-input.json'); } catch (error) { get('backup-status').textContent = error.message; } });
    get('backup-output-download').addEventListener('click', () => { if (lastResult) download(lastResult, 'backup-power-output.json'); });
    get('backup-print').addEventListener('click', () => { if (lastResult?.status === 'ok') window.print(); else get('backup-status').textContent = 'Generate a feasible roster before printing.'; });
    const importer = get('backup-import');
    if (importer) {
        const parts = new Intl.DateTimeFormat('en', { timeZone: 'Asia/Dhaka', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
        const p = type => parts.find(x => x.type === type).value;
        get('backup-date').value = p('year') + '-' + p('month') + '-' + p('day');
        fetch('/api/services/zones', { cache: 'no-store' }).then(r => { if (!r.ok) throw new Error(); return r.json(); }).then(zones => {
            const select = get('backup-zone'); select.replaceChildren();
            for (const zone of zones) { const option = node('option', zone.name + ' (' + zone.code + ')'); option.value = zone.id; select.append(option); }
        }).catch(() => { get('backup-source').textContent = 'Zones could not load. Manual JSON planning is still available. Reload to retry.'; });
        const cancelImport = () => { invalidate(); get('backup-source').textContent = 'Selection changed. Import this zone/date before generating a schedule-based plan.'; };
        get('backup-zone').addEventListener('change', cancelImport); get('backup-date').addEventListener('change', cancelImport);
        importer.addEventListener('click', async () => {
            invalidate('Loading the published plan…'); const attempt = revision;
            const controller = new AbortController(); pending = controller;
            const timeout = setTimeout(() => controller.abort(new Error('Published-plan request timed out. Retry or use reviewed manual hours.')), 8000);
            try {
                const scenario = read();
                const response = await fetch('/api/backup-power/published-grid?zoneId=' + encodeURIComponent(get('backup-zone').value) + '&date=' + encodeURIComponent(get('backup-date').value), { credentials: 'same-origin', cache: 'no-store', signal: controller.signal });
                if (response.redirected || response.status === 401 || response.status === 403) throw new Error('Sign in as a resident or electricity official to import a plan.');
                const data = await response.json(); if (attempt !== revision) return;
                if (!response.ok) throw new Error(data.message || 'Published plan unavailable.');
                scenario.grid_available = data.grid_available; write(scenario);
                get('backup-source').textContent = data.source + ' · ' + data.zone_name + ' · ' + data.date + ' · Published ' + data.published_at_utc + '. ' + data.message;
                get('backup-status').textContent = 'Published outage hours imported. Review generator/shop inputs, then generate a new roster.';
            } catch (error) {
                if (attempt === revision && error.name !== 'AbortError') { get('backup-source').textContent = error.message; get('backup-status').textContent = 'No published hours imported; existing JSON was kept. Manual hours remain your responsibility.'; }
            } finally { clearTimeout(timeout); if (attempt === revision) pending = null; }
        });
    }
    write(example);
})();
