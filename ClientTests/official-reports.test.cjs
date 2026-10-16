const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/official-reports.js'), 'utf8');

function node(tag = 'div') {
    const events = new Map();
    return {
        tag, children: [], attributes: {}, dataset: {}, hidden: false, disabled: false, ownText: '',
        set textContent(value) { this.ownText = String(value); this.children = []; },
        get textContent() { return this.ownText + this.children.map(c => c.textContent).join(''); },
        set innerHTML(value) { assert.fail('Untrusted text must never be inserted as HTML'); },
        appendChild(child) { this.children.push(child); child.parentElement = this; return child; },
        replaceChildren() { this.children = []; this.ownText = ''; },
        setAttribute(key, value) { this.attributes[key] = value; }, removeAttribute(key) { delete this.attributes[key]; },
        addEventListener(key, fn) { events.set(key, fn); }, fire(key) { return events.get(key)?.(); }
    };
}
const report = title => ({ id: 1, code: 'R-000001', title, category: 'RoadDamage', areaName: null,
    status: 'InProgress', urgency: 'High', createdAt: '2026-10-01T00:00:00' });
const page = (values = {}) => ({ page: 1, pageSize: 20, totalCount: 45, totalPages: 3, items: [report('Flooded road')], ...values });
const ok = data => ({ ok: true, status: 200, json: async () => data });
const flush = () => new Promise(done => setImmediate(done));

function setup(fetcher = async () => ok(page()), search = '') {
    const nodes = new Map();
    const get = id => { if (!nodes.has(id)) nodes.set(id, node()); return nodes.get(id); };
    const sorts = ['date', 'status', 'urgency'].map(sort => {
        const button = node('button'); button.dataset.sort = sort; button.parentElement = node('th'); return button;
    });
    get('official-reports').querySelectorAll = () => sorts;
    const windowEvents = new Map();
    const requests = [];
    const navigations = [];
    const history = [];
    const window = {
        location: { search, assign: url => navigations.push(url) },
        history: { pushState: (state, title, url) => history.push(url) },
        addEventListener: (name, handler) => windowEvents.set(name, handler)
    };
    runInNewContext(script, {
        document: { getElementById: get, createElement: tag => node(tag) }, window, URLSearchParams, AbortController,
        fetch: async (url, options) => { requests.push({ url, options }); return fetcher(url, options); }
    });
    return { get, sorts, requests, navigations, history, window, event: (name, data) => windowEvents.get(name)?.(data) };
}

test('shows loading state then safely renders report values, UTC date, and pagination', async () => {
    let resolve;
    const s = setup(() => new Promise(done => { resolve = done; }));
    assert.equal(s.get('official-reports').attributes['aria-busy'], 'true');
    assert.equal(s.get('reports-table-wrap').hidden, true);
    assert.equal(s.sorts[0].disabled, true);
    resolve(ok(page({ items: [report('<img src=x onerror=alert(1)>')] })));
    await flush();
    const row = s.get('reports-rows').children[0];
    assert.equal(row.children[1].textContent, '<img src=x onerror=alert(1)>');
    assert.equal(row.children[1].children.length, 0);
    assert.equal(row.children[3].textContent, 'Unclassified');
    assert.equal(row.children[4].textContent, 'In Progress');
    assert.equal(row.children[6].textContent, '2026-10-01 00:00');
    assert.equal(s.get('reports-page-label').textContent, 'Page 1 of 3');
    assert.equal(s.get('reports-previous').disabled, true);
    assert.equal(s.get('reports-next').disabled, false);
    assert.equal(s.requests[0].options.credentials, 'same-origin');
    assert.equal(s.requests[0].options.cache, 'no-store');
});

test('paging preserves filters, updates browser history, and sorting resets the page', async () => {
    const s = setup(async url => ok(page({ page: Number(new URL(url, 'http://local').searchParams.get('page')) })),
        '?page=2&categories=RoadDamage&categories=WaterLogging&areaId=1');
    await flush();
    s.get('reports-next').fire('click'); await flush();
    const next = new URL(s.requests.at(-1).url, 'http://local').searchParams;
    assert.equal(next.get('page'), '3');
    assert.deepEqual(next.getAll('categories'), ['RoadDamage', 'WaterLogging']);
    assert.equal(next.get('areaId'), '1');
    assert.equal(s.get('reports-next').disabled, true);
    s.sorts[2].fire('click'); await flush();
    const sorted = new URL(s.requests.at(-1).url, 'http://local').searchParams;
    assert.equal(sorted.get('page'), '1'); assert.equal(sorted.get('sort'), 'urgency');
    assert.equal(sorted.get('direction'), 'desc');
    s.sorts[2].fire('click'); await flush();
    assert.equal(s.sorts[2].parentElement.attributes['aria-sort'], 'ascending');
    assert.ok(s.history.length >= 3);
});

test('empty results show an empty state without stale rows or pagination', async () => {
    const s = setup(async () => ok(page({ items: [], totalCount: 0, totalPages: 0 })));
    await flush();
    assert.equal(s.get('reports-empty').hidden, false);
    assert.equal(s.get('reports-table-wrap').hidden, true);
    assert.equal(s.get('reports-pagination').hidden, true);
    assert.equal(s.get('reports-summary').textContent, '0 reports found.');
});

test('401 navigates to login with the report-list return URL', async () => {
    const s = setup(async () => ({ ok: false, status: 401 })); await flush();
    assert.match(s.navigations[0], /^\/login.html\?returnUrl=%2Fofficial%2Freports/);
    assert.equal(s.get('reports-table-wrap').hidden, true);
});

test('403 shows a permission message without a misleading retry', async () => {
    const s = setup(async () => ({ ok: false, status: 403 })); await flush();
    assert.match(s.get('reports-error').textContent, /permission/);
    assert.equal(s.get('reports-error').hidden, false);
    assert.equal(s.get('reports-retry').hidden, true);
});

test('invalid filters show a reset action', async () => {
    const s = setup(async () => ({ ok: false, status: 400 })); await flush();
    assert.equal(s.get('reports-reset').hidden, false);
    assert.equal(s.get('reports-retry').hidden, true);
    assert.match(s.get('reports-error').textContent, /invalid/);
});

test('network failure can be retried without changing the requested page', async () => {
    let offline = true;
    const s = setup(async () => { if (offline) throw new TypeError('Offline'); return ok(page()); });
    await flush();
    assert.equal(s.get('reports-retry').hidden, false);
    assert.equal(s.get('reports-table-wrap').hidden, true);
    offline = false; s.get('reports-retry').fire('click'); await flush();
    assert.equal(s.requests[0].url, s.requests[1].url);
    assert.equal(s.get('reports-table-wrap').hidden, false);
    assert.equal(s.get('reports-error').hidden, true);
});

test('a delayed older response cannot overwrite the current page', async () => {
    const pending = [];
    const s = setup(() => new Promise(resolve => pending.push(resolve)));
    s.window.location.search = '?page=2'; s.event('popstate');
    assert.equal(s.requests[0].options.signal.aborted, true);
    pending[1](ok(page({ page: 2, items: [report('New page')] }))); await flush();
    pending[0](ok(page({ items: [report('Old page')] }))); await flush();
    assert.equal(s.get('reports-rows').children[0].children[1].textContent, 'New page');
});

test('navigation aborts pending requests', () => {
    const s = setup(() => new Promise(() => {}));
    s.event('pagehide');
    assert.equal(s.requests[0].options.signal.aborted, true);
});

test('returning to a cached page reloads the current URL rather than retaining an interrupted load', async () => {
    const s = setup(); await flush();
    s.event('pagehide');
    s.event('pageshow', { persisted: true }); await flush();
    assert.equal(s.requests.length, 2);
    assert.equal(s.get('official-reports').attributes['aria-busy'], undefined);
    assert.equal(s.get('reports-table-wrap').hidden, false);
});
