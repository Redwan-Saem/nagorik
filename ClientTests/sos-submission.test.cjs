const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/sos-submission.js'), 'utf8');
const requestId = '11111111-1111-4111-8111-111111111111';
const response = (status, data) => ({ status, ok: status >= 200 && status < 300, json: async () => data });

test('default browser fetch keeps its global receiver during lookup and creation', async () => {
    const calls = [];
    const context = {
        browserGlobal: true, AbortController, DOMException, setTimeout, clearTimeout,
        fetch: async function (url, options) {
            assert.equal(this.browserGlobal, true, 'native fetch must not receive SosSubmission as its receiver');
            calls.push({ url, options });
            return options.method === 'POST' ? response(201, { id: 7, status: 'Active' }) : response(404);
        }
    };
    runInNewContext(script, context);
    const submission = new context.SosSubmission({
        storage: { getItem: () => null, setItem() {}, removeItem() {} },
        uuid: () => requestId, online: () => true, attempts: 1
    });
    assert.equal((await submission.submit({ addressText: 'Bosila' })).id, 7);
    assert.equal(calls.length, 2);
    assert.equal(calls[1].options.method, 'POST');
});

function setup(handler, options = {}) {
    const context = { AbortController, DOMException, setTimeout, clearTimeout };
    runInNewContext(script, context);
    const calls = [];
    const waits = [];
    const storage = new Map(options.existingReference ? [['sos-pending-request', requestId]] : []);
    const submission = new context.SosSubmission({
        fetcher: async (url, request) => { calls.push({ url, request }); return handler(url, request, calls); },
        storage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
        uuid: () => requestId, online: () => true,
        sleep: async (ms, signal) => { waits.push(ms); signal.throwIfAborted(); }, ...options
    });
    return { submission, calls, waits, storage };
}

test('lost creation response recovers the saved alert without a second POST', async () => {
    let saved = false;
    const state = setup((url, options) => {
        if (options.method === 'POST') { saved = true; throw new TypeError('connection lost after commit'); }
        return saved ? response(200, { id: 7, status: 'Active' }) : response(404);
    });
    const alert = await state.submission.submit({ addressText: 'Dhaka' });
    assert.equal(alert.id, 7);
    assert.equal(state.calls.filter(c => c.request.method === 'POST').length, 1);
    assert.deepEqual(state.waits, [1000]);
    assert.equal(state.storage.size, 0);
});

test('recovers a cancelled alert after reload instead of triggering a new emergency', async () => {
    const state = setup(() => response(200, { id: 7, status: 'Cancelled' }), { existingReference: true });
    const alert = await state.submission.submit({ addressText: 'Dhaka' });
    assert.equal(alert.status, 'Cancelled');
    assert.equal(state.calls.length, 1);
    assert.match(state.calls[0].url, /requests\/11111111/);
    assert.equal(state.calls.some(c => c.request.method === 'POST'), false);
});

test('retries transient failures with bounded backoff and one stable submission reference', async () => {
    let posts = 0;
    const state = setup((url, options) => {
        if (!options.method) return response(404);
        return ++posts < 3 ? response(503) : response(201, { id: 7, status: 'Active' });
    });
    await state.submission.submit({ addressText: 'Dhaka' });
    assert.deepEqual(state.waits, [1000, 2000]);
    const ids = state.calls.filter(c => c.request.method === 'POST').map(c => JSON.parse(c.request.body).clientRequestId);
    assert.deepEqual(ids, [requestId, requestId, requestId]);
});

test('exhausted attempts retain only the reference for a later explicit retry', async () => {
    const state = setup(() => response(503));
    await assert.rejects(state.submission.submit({ addressText: 'Private address' }));
    assert.equal(state.calls.length, 3);
    assert.deepEqual(state.waits, [1000, 2000]);
    assert.equal(state.storage.get('sos-pending-request'), requestId);
    assert.equal(JSON.stringify([...state.storage]).includes('Private address'), false);
    assert.equal(state.calls.some(c => c.request.method === 'POST'), false); // Lookup failed, so no mutation.
});

test('unconfirmed lookup never permits a blind creation retry', async () => {
    const state = setup(() => { throw new TypeError('offline'); });
    await assert.rejects(state.submission.submit({ addressText: 'Dhaka' }));
    assert.equal(state.calls.filter(c => c.request.method === 'POST').length, 0);
});

for (const status of [400, 401, 403, 409]) {
    test(`does not retry HTTP ${status}`, async () => {
        const state = setup(() => response(status));
        await assert.rejects(state.submission.submit({ addressText: 'Dhaka' }), error => error.status === status);
        assert.equal(state.calls.length, 1);
        assert.equal(state.waits.length, 0);
    });
}

test('double submits share the pending operation', async () => {
    let resolve;
    const state = setup(() => new Promise(done => { resolve = done; }));
    const first = state.submission.submit({ addressText: 'Dhaka' });
    const second = state.submission.submit({ addressText: 'Dhaka' });
    assert.equal(first, second);
    assert.equal(state.calls.length, 1);
    resolve(response(200, { id: 7, status: 'Active' }));
    await first;
});

test('offline submission makes no request or background replay', async () => {
    const state = setup(() => assert.fail('must not fetch'), { online: () => false });
    await assert.rejects(state.submission.submit({ addressText: 'Dhaka' }), /offline/);
    assert.equal(state.calls.length, 0);
});

test('timeout after save recovers the exact alert and does not send another POST', async () => {
    let saved = false;
    const state = setup((url, options) => {
        if (options.method === 'POST') { saved = true; return new Promise(() => {}); }
        return saved ? response(200, { id: 7, status: 'Completed' }) : response(404);
    }, { timeoutMs: 10 });
    const alert = await state.submission.submit({ addressText: 'Dhaka' });
    assert.equal(alert.status, 'Completed');
    assert.equal(state.calls.filter(c => c.request.method === 'POST').length, 1);
});

test('page exit cancels backoff and prevents another attempt', async () => {
    let unblock;
    const state = setup(() => response(503), {
        sleep: (ms, signal) => new Promise((resolve, reject) => {
            unblock = () => reject(new DOMException('stopped', 'AbortError'));
            signal.addEventListener('abort', unblock, { once: true });
        })
    });
    const pending = state.submission.submit({ addressText: 'Dhaka' });
    await new Promise(done => setImmediate(done));
    state.submission.cancel();
    await assert.rejects(pending, error => error.name === 'AbortError');
    assert.equal(state.calls.length, 1);
});
