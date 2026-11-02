const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/sos-live-location.js'), 'utf8');

test('default browser fetch keeps its global receiver for ownership and GPS updates', async () => {
    const requests = [];
    let position;
    const context = {
        browserGlobal: true, AbortController, navigator: {},
        setInterval: () => 1, clearInterval() {},
        fetch: async function (url, options) {
            assert.equal(this.browserGlobal, true, 'native fetch must not receive SosLiveLocation as its receiver');
            requests.push({ url, options });
            return { ok: true, status: 200, json: async () => ({ id: 5, status: 'Active', addressText: 'Bosila' }) };
        }
    };
    runInNewContext(script, context);
    const tracker = new context.SosLiveLocation({
        geolocation: { watchPosition(callback) { position = callback; return 1; }, clearWatch() {} },
        now: () => 10000
    });
    try {
        assert.equal(await tracker.start(5), true);
        await position({ coords: { latitude: 23.81, longitude: 90.41 } });
        assert.equal(requests.length, 2);
        assert.equal(requests[1].options.method, 'PATCH');
    } finally { tracker.stop(); }
});

function setup({ status = 'Active', updateStatus = 200, unavailable = false } = {}) {
    let time = 0;
    let poll;
    let success;
    let error;
    let clears = 0;
    let watches = 0;
    let currentStatus = status;
    let fail = false;
    const requests = [];
    const messages = [];
    const errors = [];
    const geolocation = {
        watchPosition(onSuccess, onError, options) {
            success = onSuccess; error = onError; watches++;
            assert.equal(options.enableHighAccuracy, true);
            return 0; // Watch IDs can be zero.
        },
        clearWatch(id) { assert.equal(id, 0); clears++; }
    };
    const context = {
        navigator: {}, AbortController,
        setInterval(callback, ms) { assert.equal(ms, 15000); poll = callback; return 0; },
        clearInterval() { poll = undefined; }
    };
    runInNewContext(script, context);
    const tracker = new context.SosLiveLocation({
        geolocation: unavailable ? null : geolocation,
        now: () => time, onStatus: text => messages.push(text), onError: text => errors.push(text),
        fetcher: async (url, options) => {
            requests.push({ url, options });
            if (fail) throw new Error('offline');
            if (options.method === 'PATCH') return { ok: updateStatus === 200, status: updateStatus };
            return { ok: true, status: 200, json: async () => ({ id: 5, status: currentStatus, addressText: 'Dhaka' }) };
        }
    });
    return {
        tracker, requests, errors, messages,
        setTime(value) { time = value; }, setStatus(value) { currentStatus = value; },
        setOffline(value) { fail = value; },
        position(lat, lon) { return success({ coords: { latitude: lat, longitude: lon } }); },
        gpsError(code) { error({ code }); }, poll() { return poll?.(); },
        get updates() { return requests.filter(r => r.options.method === 'PATCH'); },
        get clears() { return clears; }, get watches() { return watches; }
    };
}

test('verifies active ownership before starting GPS and bounds location updates', async () => {
    const state = setup();
    assert.equal(await state.tracker.start(5), true);
    assert.equal(state.requests[0].url, '/api/sos/5');
    await state.position(23.81, 90.41);
    assert.equal(state.updates.length, 1);
    assert.equal(state.updates[0].options.credentials, 'same-origin');
    assert.deepEqual(JSON.parse(state.updates[0].options.body), { latitude: 23.81, longitude: 90.41, addressText: 'Dhaka' });
    state.setTime(1000);
    await state.position(23.82, 90.42);
    assert.equal(state.updates.length, 1);
    state.setTime(10000);
    await state.position(23.81001, 90.41001);
    assert.equal(state.updates.length, 1); // Too little movement.
    state.setTime(11000);
    await state.position(23.82, 90.42);
    assert.equal(state.updates.length, 2);
    state.setTime(41000);
    await state.position(23.82, 90.42);
    assert.equal(state.updates.length, 3); // A later fix can refresh a stationary location.
    state.tracker.stop();
    assert.equal(state.clears, 1);
    assert.equal(state.updates[0].options.signal.aborted, true);
    await state.position(23.83, 90.43);
    assert.equal(state.updates.length, 3);
});

for (const status of ['Cancelled', 'Completed']) {
    test(`does not watch GPS for ${status} alerts`, async () => {
        const state = setup({ status });
        assert.equal(await state.tracker.start(5), false);
        assert.equal(state.watches, 0);
        assert.match(state.errors[0], /no longer active/);
    });
    test(`stops live tracking when polling detects ${status}`, async () => {
        const state = setup();
        await state.tracker.start(5);
        state.setStatus(status);
        await state.poll();
        assert.equal(state.tracker.running, false);
        assert.equal(state.clears, 1);
        assert.match(state.messages.at(-1), /Live location sharing stopped/);
    });
}

test('denied GPS permission stops tracking and offers a manual address', async () => {
    const state = setup();
    await state.tracker.start(5);
    state.gpsError(1);
    assert.equal(state.tracker.running, false);
    assert.equal(state.clears, 1);
    assert.match(state.errors.at(-1), /manual address/);
});

test('missing GPS uses manual fallback without requesting a watch', async () => {
    const state = setup({ unavailable: true });
    assert.equal(await state.tracker.start(5), false);
    assert.match(state.errors.at(-1), /manual address/);
    assert.equal(state.watches, 0);
});

for (const updateStatus of [401, 403, 404, 409]) {
    test(`stops GPS on update HTTP ${updateStatus}`, async () => {
        const state = setup({ updateStatus });
        await state.tracker.start(5);
        await state.position(23.81, 90.41);
        assert.equal(state.tracker.running, false);
        assert.equal(state.clears, 1);
        assert.match(state.errors.at(-1), /Refresh the page/);
    });
}

test('offline updates are throttled and do not report a saved location', async () => {
    const state = setup();
    await state.tracker.start(5);
    state.setOffline(true);
    await state.position(23.81, 90.41);
    assert.match(state.errors.at(-1), /offline/);
    state.setTime(1000);
    await state.position(23.82, 90.42);
    assert.equal(state.updates.length, 1);
    assert.equal(state.messages.some(text => text.startsWith('GPS updated')), false);
    state.setTime(10000);
    state.setOffline(false);
    await state.position(23.82, 90.42);
    assert.equal(state.updates.length, 2);
    assert.match(state.messages.at(-1), /GPS updated/);
});

test('ignores invalid coordinate fixes', async () => {
    const state = setup();
    await state.tracker.start(5);
    await state.position(NaN, 90);
    await state.position(91, 90);
    await state.position(23, Infinity);
    assert.equal(state.updates.length, 0);
});
