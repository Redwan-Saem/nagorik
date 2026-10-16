const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext, createContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/sos-trigger.js'), 'utf8');
const submissionScript = readFileSync(join(__dirname, '../wwwroot/js/sos-submission.js'), 'utf8');

function element() {
    const listeners = new Map();
    return {
        value: '', textContent: '', hidden: false, disabled: false, open: false,
        addEventListener(name, fn) { listeners.set(name, fn); },
        fire(name, data = {}) { return listeners.get(name)?.({ preventDefault() {}, ...data }); },
        focus() { this.focused = true; }, setPointerCapture() {}, setAttribute() {}, removeAttribute() {},
        showModal() { this.open = true; }, close() { this.open = false; this.fire('close'); }
    };
}
function setup({ offline = false, noGps = false, responseStatus = 201, fetcher } = {}) {
    const elements = new Map();
    const get = id => { if (!elements.has(id)) elements.set(id, element()); return elements.get(id); };
    const requests = [];
    const navigations = [];
    const timers = new Map();
    let time = 0;
    let nextTimer = 1;
    let gpsSuccess;
    let gpsError;
    const window = element();
    window.location = { assign: url => navigations.push(url) };
    const navigator = { onLine: !offline };
    if (!noGps) navigator.geolocation = { getCurrentPosition(ok, fail) { gpsSuccess = ok; gpsError = fail; } };
    const context = createContext({
        document: { getElementById: get }, navigator, window,
        AbortController, DOMException, setTimeout, clearTimeout,
        crypto: { randomUUID: () => '11111111-1111-4111-8111-111111111111' },
        Date: { now: () => time },
        setInterval(fn) { const id = nextTimer++; timers.set(id, fn); return id; },
        clearInterval(id) { timers.delete(id); },
        fetch: async (url, options) => {
            if (!options.method) return { status: 404, ok: false };
            requests.push({ url, options });
            return fetcher ? fetcher(url, options) : { ok: responseStatus === 201, status: responseStatus, json: async () => ({ id: 7, status: 'Active' }) };
        }
    });
    runInNewContext(submissionScript, context);
    const Submission = context.SosSubmission;
    context.SosSubmission = class extends Submission { constructor(options) { super({ ...options, sleep: async () => {} }); } };
    runInNewContext(script, context);
    return {
        get, requests, navigations, navigator,
        tick(ms) { time += ms; for (const fn of [...timers.values()]) fn(); },
        gpsSuccess(lat, lon) { gpsSuccess({ coords: { latitude: lat, longitude: lon } }); },
        gpsDenied() { gpsError({ code: 1 }); }
    };
}
function manual(state) { state.get('sos-trigger-address').value = 'House 5, Dhaka'; }
function press(state) { state.get('sos-hold').fire('pointerdown', { button: 0, pointerId: 1, isPrimary: true }); }
function release(state) { state.get('sos-hold').fire('pointerup', { pointerId: 1 }); }

test('quick taps and interrupted holds never open review or send an alert', async () => {
    const s = setup(); manual(s);
    press(s); s.tick(300); release(s);
    assert.equal(s.get('sos-review-dialog').open, false);
    press(s); s.tick(2200); s.get('sos-hold').fire('pointercancel');
    release(s);
    assert.equal(s.get('sos-review-dialog').open, false);
    await s.get('sos-confirm').fire('click');
    assert.equal(s.requests.length, 0);
});

test('a deliberate hold requires release, review, and separate confirmation', async () => {
    const s = setup(); manual(s);
    press(s); s.tick(2000);
    assert.equal(s.get('sos-review-dialog').open, false);
    assert.equal(s.get('sos-hold-progress').value, 100);
    release(s);
    assert.equal(s.get('sos-review-dialog').open, true);
    assert.equal(s.requests.length, 0);
    assert.equal(s.get('sos-review-back').focused, true);
    await s.get('sos-confirm').fire('click');
    assert.equal(s.requests.length, 1);
    assert.deepEqual(JSON.parse(s.requests[0].options.body), { addressText: 'House 5, Dhaka', clientRequestId: '11111111-1111-4111-8111-111111111111' });
    assert.equal(s.requests[0].options.credentials, 'same-origin');
    assert.deepEqual(s.navigations, ['/sos/7']);
});

test('keyboard hold and the no-hold alternative open review without sending', () => {
    const s = setup(); manual(s);
    s.get('sos-hold').fire('keydown', { key: ' ', repeat: false });
    s.tick(2000);
    s.get('sos-hold').fire('keydown', { key: ' ', repeat: true });
    assert.equal(s.get('sos-review-dialog').open, false);
    s.get('sos-hold').fire('keyup', { key: ' ' });
    assert.equal(s.get('sos-review-dialog').open, true);
    s.get('sos-review-back').fire('click');
    s.get('sos-review').fire('click');
    assert.equal(s.get('sos-review-dialog').open, true);
    assert.equal(s.requests.length, 0);
});

test('missing location blocks review and denied GPS accepts a manual address', () => {
    const s = setup();
    s.get('sos-review').fire('click');
    assert.equal(s.get('sos-review-dialog').open, false);
    s.get('sos-use-gps').fire('click');
    s.gpsDenied();
    assert.match(s.get('sos-gps-message').textContent, /manual address/);
    manual(s); s.get('sos-review').fire('click');
    assert.equal(s.get('sos-review-dialog').open, true);
});

test('late GPS cannot change the location already reviewed', async () => {
    const s = setup(); manual(s);
    s.get('sos-use-gps').fire('click');
    s.get('sos-review').fire('click');
    s.gpsSuccess(23.81, 90.41);
    await s.get('sos-confirm').fire('click');
    assert.equal(JSON.parse(s.requests[0].options.body).addressText, 'House 5, Dhaka');
    assert.equal(JSON.parse(s.requests[0].options.body).latitude, undefined);
});

test('GPS can be selected then explicitly cleared for a manual fallback', async () => {
    const s = setup(); manual(s);
    s.get('sos-use-gps').fire('click'); s.gpsSuccess(23.81, 90.41);
    s.get('sos-clear-gps').fire('click');
    s.get('sos-review').fire('click'); await s.get('sos-confirm').fire('click');
    assert.equal(JSON.parse(s.requests[0].options.body).addressText, 'House 5, Dhaka');
    assert.equal(JSON.parse(s.requests[0].options.body).latitude, undefined);
});

test('double confirmation while saving submits only once', async () => {
    let resolve;
    const s = setup({ fetcher: () => new Promise(done => { resolve = done; }) }); manual(s);
    s.get('sos-review').fire('click');
    const pending = s.get('sos-confirm').fire('click');
    await s.get('sos-confirm').fire('click');
    await new Promise(done => setImmediate(done));
    assert.equal(s.requests.length, 1);
    assert.equal(s.get('sos-review-back').disabled, true);
    resolve({ ok: true, status: 201, json: async () => ({ id: 7, status: 'Active' }) });
    await pending;
});

test('offline mode sends nothing and offers a clear connection error', async () => {
    const s = setup({ offline: true }); manual(s);
    s.get('sos-review').fire('click'); await s.get('sos-confirm').fire('click');
    assert.equal(s.requests.length, 0);
    assert.match(s.get('sos-send-error').textContent, /offline/);
});

for (const responseStatus of [401, 409, 500]) {
    test(`handles HTTP ${responseStatus} without claiming successful dispatch`, async () => {
        const s = setup({ responseStatus }); manual(s);
        s.get('sos-review').fire('click'); await s.get('sos-confirm').fire('click');
        if (responseStatus === 409) assert.deepEqual(s.navigations, ['/sos/active']);
        else {
            assert.equal(s.get('sos-check-active').hidden, false);
            assert.equal(s.get('sos-confirm').disabled, true);
            assert.equal(s.navigations.length, 0);
        }
        if (responseStatus === 401) assert.equal(s.get('sos-login').hidden, false);
    });
}
