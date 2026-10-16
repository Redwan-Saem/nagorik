const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/sos-active.js'), 'utf8');

function setup({ ok = true, fetcher } = {}) {
    const nodes = new Map();
    const get = id => {
        if (!nodes.has(id)) {
            const events = new Map();
            nodes.set(id, {
                dataset: { alertId: '9' }, textContent: '', value: 'Dhaka', disabled: false, open: false,
                addEventListener: (name, handler) => events.set(name, handler),
                fire(name) { return events.get(name)?.({ currentTarget: this, preventDefault() {} }); },
                focus() { this.focused = true; }, showModal() { this.open = true; }, close() { this.open = false; },
                querySelector() { return get('manual-submit'); }, reportValidity() { return true; },
                setAttribute() {}, removeAttribute() {}
            });
        }
        return nodes.get(id);
    };
    let stopped = 0;
    let tracker;
    const requests = [];
    const navigations = [];
    runInNewContext(script, {
        document: { getElementById: get },
        window: { addEventListener() {}, location: { assign: url => navigations.push(url) } },
        SosLiveLocation: class {
            constructor(options) { this.options = options; this.running = false; tracker = this; }
            async start() { this.running = true; }
            stop() { stopped++; this.running = false; }
        },
        fetch: async (url, options) => {
            requests.push({ url, options });
            return fetcher ? fetcher() : { ok, json: async () => ({ status: 'Cancelled' }) };
        }
    });
    return { get, requests, navigations, get stopped() { return stopped; }, get tracker() { return tracker; } };
}

test('opening cancellation and choosing Keep alert active makes no request and preserves tracking', () => {
    const s = setup();
    s.get('sos-cancel').fire('click');
    assert.equal(s.get('sos-cancel-dialog').open, true);
    assert.equal(s.get('sos-cancel-back').focused, true);
    assert.equal(s.requests.length, 0);
    s.get('sos-cancel-back').fire('click');
    assert.equal(s.get('sos-cancel-dialog').open, false);
    assert.equal(s.requests.length, 0);
    assert.equal(s.stopped, 0);
});

test('confirmed cancellation stops tracking, submits once, and opens the saved result', async () => {
    let resolve;
    const s = setup({ fetcher: () => new Promise(done => { resolve = done; }) });
    await s.get('sos-cancel-confirm').fire('click');
    assert.equal(s.requests.length, 0); // Cannot bypass the dialog.
    s.get('sos-cancel').fire('click');
    const pending = s.get('sos-cancel-confirm').fire('click');
    await s.get('sos-cancel-confirm').fire('click');
    assert.equal(s.stopped, 1);
    assert.equal(s.requests.length, 1);
    assert.equal(s.requests[0].url, '/api/sos/9/cancel');
    assert.equal(s.requests[0].options.method, 'POST');
    assert.equal(s.requests[0].options.credentials, 'same-origin');
    assert.equal(s.get('sos-cancel-back').disabled, true);
    resolve({ ok: true, json: async () => ({ status: 'Cancelled' }) });
    await pending;
    assert.deepEqual(s.navigations, ['/sos/9']);
    assert.equal(s.get('start-sos-tracking').disabled, true);
});

test('failed cancellation displays uncertainty without claiming the alert was cancelled', async () => {
    const s = setup({ ok: false });
    s.get('sos-cancel').fire('click');
    await s.get('sos-cancel-confirm').fire('click');
    assert.match(s.get('sos-cancel-error').textContent, /Cancellation could not be confirmed/);
    assert.equal(s.navigations.length, 0);
    assert.equal(s.stopped, 1);
    assert.equal(s.get('sos-cancel-confirm').disabled, true);
});

test('terminal status disables all further update and cancellation actions', () => {
    const s = setup();
    s.tracker.options.onAlert({ status: 'Cancelled' });
    assert.equal(s.get('sos-status').textContent, 'Cancelled');
    assert.equal(s.get('start-sos-tracking').disabled, true);
    assert.equal(s.get('manual-submit').disabled, true);
    s.get('sos-cancel').fire('click');
    assert.equal(s.get('sos-cancel-dialog').open, false);
});
