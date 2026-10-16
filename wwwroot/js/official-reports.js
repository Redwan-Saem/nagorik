(() => {
    const root = document.getElementById('official-reports');
    if (!root) return;
    const get = id => document.getElementById(id);
    const summary = get('reports-summary');
    const error = get('reports-error');
    const retry = get('reports-retry');
    const reset = get('reports-reset');
    const empty = get('reports-empty');
    const table = get('reports-table-wrap');
    const rows = get('reports-rows');
    const pagination = get('reports-pagination');
    const previous = get('reports-previous');
    const next = get('reports-next');
    const sorts = [...root.querySelectorAll('[data-sort]')];
    let params;
    let sequence = 0;
    let controller;
    let page;
    function cell(row, text) {
        const td = document.createElement('td');
        td.textContent = text ?? '';
        row.appendChild(td);
        return td;
    }
    function render(items) {
        rows.replaceChildren();
        for (const report of items) {
            const row = document.createElement('tr');
            cell(row, report.code); cell(row, report.title);
            cell(row, ({ RoadDamage: 'Road damage', WaterLogging: 'Waterlogging', PowerOutage: 'Power outage' })[report.category] || report.category);
            cell(row, report.areaName ?? 'Unclassified');
            const status = document.createElement('span'); status.className = 'badge';
            status.textContent = report.status === 'InProgress' ? 'In Progress' : report.status;
            cell(row, '').appendChild(status);
            const urgency = document.createElement('span');
            urgency.className = `badge ${report.urgency === 'Critical' ? 'badge-bad' : report.urgency === 'High' ? 'badge-warn' : 'badge-muted'}`;
            urgency.textContent = report.urgency; cell(row, '').appendChild(urgency);
            const value = report.createdAt || '';
            const date = new Date(/(?:Z|[+-]\d\d:\d\d)$/.test(value) ? value : `${value}Z`);
            const time = document.createElement('time');
            if (Number.isNaN(date.getTime())) time.textContent = 'Date unavailable';
            else { time.dateTime = date.toISOString(); time.textContent = date.toISOString().slice(0, 16).replace('T', ' '); }
            cell(row, '').appendChild(time); rows.appendChild(row);
        }
    }
    async function load(values, push = false) {
        params = new URLSearchParams(values);
        if (!params.has('page')) params.set('page', '1');
        if (!params.has('sort')) params.set('sort', 'date');
        if (!params.has('direction')) params.set('direction', 'desc');
        const requested = new URLSearchParams(params);
        const current = ++sequence;
        controller?.abort(); controller = new AbortController();
        root.setAttribute('aria-busy', 'true');
        summary.textContent = 'Loading reports…';
        error.hidden = retry.hidden = reset.hidden = true;
        table.hidden = empty.hidden = pagination.hidden = true;
        previous.disabled = next.disabled = true;
        sorts.forEach(button => { button.disabled = true; });
        try {
            const response = await fetch(`/api/reports?${requested}`, {
                credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' }, signal: controller.signal
            });
            if (current !== sequence) return;
            if (response.status === 401) {
                window.location.assign(`/login.html?returnUrl=${encodeURIComponent(`/official/reports?${requested}`)}`); return;
            }
            if (!response.ok) {
                const failure = new Error(response.status === 403 ? 'You do not have permission to view official reports.' :
                    response.status === 400 ? 'The report filters or page are invalid. Reset the report list to continue.' :
                    'Reports could not be loaded. Check your connection and retry.');
                failure.retryable = response.status >= 500; throw failure;
            }
            const data = await response.json();
            if (current !== sequence) return;
            if (!Array.isArray(data.items) || !Number.isInteger(data.page) || data.page < 1 || data.pageSize !== 20 ||
                !Number.isInteger(data.totalCount) || !Number.isInteger(data.totalPages))
                throw new Error('The report response could not be read. Retry loading reports.');
            page = data;
            render(data.items);
            table.hidden = data.items.length === 0;
            empty.hidden = data.items.length !== 0;
            empty.textContent = data.totalCount === 0 ? 'No reports match this selection.' : 'There are no reports on this page. Go to the previous page or reset the report list.';
            summary.textContent = `${data.totalCount} report${data.totalCount === 1 ? '' : 's'} found.`;
            pagination.hidden = data.totalCount === 0;
            get('reports-page-label').textContent = data.page <= data.totalPages ? `Page ${data.page} of ${data.totalPages}` :
                `Page ${data.page} (last page ${data.totalPages})`;
            previous.disabled = data.page <= 1; next.disabled = data.page >= data.totalPages;
            const sort = requested.get('sort'); const direction = requested.get('direction');
            sorts.forEach(button => {
                button.parentElement.setAttribute('aria-sort', button.dataset.sort === sort ? direction === 'asc' ? 'ascending' : 'descending' : 'none');
            });
            if (push) window.history.pushState({}, '', `/official/reports?${requested}`);
        } catch (failure) {
            if (current !== sequence || failure.name === 'AbortError') return;
            summary.textContent = 'Reports are unavailable.';
            error.textContent = failure.message; error.hidden = false;
            retry.hidden = failure.retryable === false; reset.hidden = false;
        } finally {
            if (current === sequence) {
                root.removeAttribute('aria-busy'); sorts.forEach(button => { button.disabled = false; });
            }
        }
    }
    previous.addEventListener('click', () => {
        if (previous.disabled || !page) return;
        const values = new URLSearchParams(params); values.set('page', String(page.page - 1)); load(values, true);
    });
    next.addEventListener('click', () => {
        if (next.disabled || !page) return;
        const values = new URLSearchParams(params); values.set('page', String(page.page + 1)); load(values, true);
    });
    sorts.forEach(button => button.addEventListener('click', () => {
        if (button.disabled) return;
        const values = new URLSearchParams(params);
        values.set('direction', values.get('sort') === button.dataset.sort && values.get('direction') === 'desc' ? 'asc' : 'desc');
        values.set('sort', button.dataset.sort); values.set('page', '1'); load(values, true);
    }));
    retry.addEventListener('click', () => load(params));
    window.addEventListener('popstate', () => load(window.location.search));
    window.addEventListener('pagehide', () => { sequence++; controller?.abort(); });
    window.addEventListener('pageshow', event => { if (event.persisted) load(window.location.search); });
    load(window.location.search);
})();
