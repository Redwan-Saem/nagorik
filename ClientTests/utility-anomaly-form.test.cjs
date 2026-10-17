const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/utility-anomaly-form.js'), 'utf8');
function element() {
    const events = new Map(); let value = '';
    return { children: [], attributes: {}, dataset: {}, files: [], hidden: false, disabled: false, textContent: '',
        get value() { return value; }, set value(v) { value = v; if (v === '') this.files = []; },
        set innerHTML(_) { assert.fail('Do not insert untrusted HTML'); },
        addEventListener(name, handler) { events.set(name, handler); },
        fire(name, event = {}) { return events.get(name)?.({ preventDefault() {}, ...event }); },
        setAttribute(name, value) { this.attributes[name] = value; },
        appendChild(child) { this.children.push(child); }, replaceChildren() { this.children = []; },
        focus() { this.focused = true; }
    };
}
function photo(name = 'photo.jpg', type = 'image/jpeg', bytes = [255, 216, 255, 224, 0, 0, 0, 0], size = bytes.length) {
    return { name, type, size, slice: (start, end) => ({ arrayBuffer: async () => Uint8Array.from(bytes.slice(start, end)).buffer }) };
}
const response = (status, body, retry = null) => ({ status, headers: { get: () => retry }, json: async () => body });
function setup(fetcher = async () => response(201, { id: 42, referenceNo: 'UA-000042' }), geolocation) {
    const nodes = new Map(), events = new Map(), calls = [], navigations = [], revoked = [], intervals = new Map(); let now = 1000, nextTimer = 0;
    const get = name => { if (!nodes.has(name)) nodes.set(name, element()); return nodes.get(name); };
    get('form').dataset.highRiskTypes = 'ExposedWire,GasLeak,FallenPole';
    get('AnomalyType').value = 'GasLeak'; get('AreaId').value = '1'; get('AddressText').value = 'Dhaka street';
    get('Description').value = 'Gas leaking near the road'; get('Photos').files = [photo()];
    class Data { constructor() { this.fields = []; } append(...values) { this.fields.push(values); } }
    let objectId = 0;
    runInNewContext(script, { document: { getElementById: id => get(id.replace(/^utility-/, '')), createElement: () => element() },
        window: { location: { assign: url => navigations.push(url) }, addEventListener: (name, handler) => events.set(name, handler) },
        navigator: { geolocation }, URL: { createObjectURL: () => 'blob:' + (++objectId), revokeObjectURL: url => revoked.push(url) },
        Uint8Array, FormData: Data, Date: { now: () => now },
        setInterval: handler => { intervals.set(++nextTimer, handler); return nextTimer; }, clearInterval: id => intervals.delete(id),
        fetch: async (url, options) => { calls.push({ url, options }); return fetcher(url, options); }
    });
    return { get, calls, navigations, revoked, events, submit: () => get('form').fire('submit'),
        advance: ms => { now += ms; [...intervals.values()].forEach(handler => handler()); } };
}
test('submits multipart fields with cookies once, then navigates to confirmation with the returned reference', async () => {
    let finish; const ui = setup(() => new Promise(resolve => { finish = resolve; }));
    const pending = ui.submit(); await new Promise(setImmediate);
    assert.equal(ui.get('submit').disabled, true); assert.equal(ui.get('fields').disabled, true);
    await ui.submit(); assert.equal(ui.calls.length, 1);
    assert.equal(ui.calls[0].url, '/api/reports/utility-anomalies'); assert.equal(ui.calls[0].options.credentials, 'same-origin');
    assert.equal(ui.calls[0].options.headers, undefined);
    assert.deepEqual(ui.calls[0].options.body.fields.map(f => f[0]), ['AnomalyType', 'AreaId', 'AddressText', 'Description', 'Photos']);
    finish(response(201, { id: 42, referenceNo: 'UA-000042' })); await pending;
    assert.deepEqual(ui.navigations, ['/Reports/Confirmation/42']); assert.match(ui.get('status').textContent, /UA-000042/);
});
test('blocks missing type, area, address and short description with accessible inline errors', async () => {
    const ui = setup(); for (const name of ['AnomalyType', 'AreaId', 'AddressText', 'Description']) ui.get(name).value = ' ';
    ui.get('AnomalyType').value = ''; ui.get('AreaId').value = '';
    await ui.submit(); assert.equal(ui.calls.length, 0);
    for (const name of ['AnomalyType', 'AreaId', 'AddressText', 'Description']) assert.equal(ui.get(name).attributes['aria-invalid'], 'true');
    assert.equal(ui.get('AnomalyType').focused, true); assert.equal(ui.get('submit').disabled, false);
});
for (const [name, files] of [
    ['zero photos', []], ['four photos', [photo(), photo(), photo(), photo()]],
    ['empty photo', [photo('x.jpg', 'image/jpeg', [])]], ['oversized photo', [photo('x.jpg', 'image/jpeg', [255, 216, 255], 5242881)]],
    ['wrong MIME', [photo('x.jpg', 'image/png')]], ['executable signature', [photo('x.jpg', 'image/jpeg', [77, 90, 0])]],
    ['PNG truncated header', [photo('x.png', 'image/png', [137, 80, 78, 71])]], ['wrong extension', [photo('x.exe')]]
]) test('rejects ' + name + ' before sending', async () => {
    const ui = setup(); ui.get('Photos').files = files; await ui.submit();
    assert.equal(ui.calls.length, 0); assert.equal(ui.get('Photos-error').hidden, false);
});
test('description boundaries and counter match the API', async () => {
    for (const length of [9, 10, 1000, 1001]) {
        const ui = setup(); ui.get('Description').value = 'x'.repeat(length); ui.get('Description').fire('input');
        assert.equal(ui.get('count').textContent, length + ' / 1000 characters');
        await ui.submit(); assert.equal(ui.calls.length, length >= 10 && length <= 1000 ? 1 : 0);
    }
});
test('previews use safe captions and revoke object URLs when replaced or removed', async () => {
    const ui = setup(); ui.get('Photos').files = [photo('<img onerror=evil>.jpg'), photo('x.png', 'image/png', [137, 80, 78, 71, 13, 10, 26, 10])];
    await ui.get('Photos').fire('change'); assert.equal(ui.get('previews').children.length, 2);
    assert.equal(ui.get('previews').children[0].children[1].textContent, '<img onerror=evil>.jpg');
    ui.get('clear-photos').fire('click'); assert.equal(ui.get('previews').children.length, 0); assert.equal(ui.revoked.length, 2);
    await ui.submit(); assert.equal(ui.calls.length, 0);
});
test('stale photo validation cannot overwrite a newer selection', async () => {
    const ui = setup(); let finish;
    const slow = photo(); slow.slice = () => ({ arrayBuffer: () => new Promise(resolve => { finish = resolve; }) });
    ui.get('Photos').files = [slow]; const pending = ui.get('Photos').fire('change');
    ui.get('Photos').files = [photo('new.jpg')]; await ui.get('Photos').fire('change');
    finish(Uint8Array.from([77, 90]).buffer); await pending;
    assert.equal(ui.get('Photos-error').hidden, true); assert.equal(ui.get('previews').children[0].children[1].textContent, 'new.jpg');
});
test('high risk warning follows the server-provided risk list', () => {
    const ui = setup();
    for (const [type, hidden] of [['ExposedWire', false], ['GasLeak', false], ['FallenPole', false], ['WaterLeak', true], ['Other', true]]) {
        ui.get('AnomalyType').value = type; ui.get('AnomalyType').fire('change'); assert.equal(ui.get('safety').hidden, hidden);
    }
});
test('GPS denial and unavailable GPS retain the address-only path', async () => {
    for (const gps of [undefined, { getCurrentPosition: (_, fail) => fail() }, { getCurrentPosition: () => { throw new Error('GPS restricted'); } }]) {
        const ui = setup(undefined, gps); ui.get('use-gps').fire('click'); await ui.submit();
        assert.equal(ui.calls.length, 1); assert.equal(ui.calls[0].options.body.fields.some(f => f[0] === 'Latitude'), false);
        assert.match(ui.get('gps-status').textContent, /address/);
    }
});
test('GPS can be captured or cleared and late callbacks cannot restore it', async () => {
    let success; const ui = setup(undefined, { getCurrentPosition: callback => { success = callback; } });
    ui.get('use-gps').fire('click'); assert.equal(ui.get('submit').disabled, true);
    ui.get('clear-gps').fire('click'); success({ coords: { latitude: 23.8, longitude: 90.4 } }); await ui.submit();
    assert.equal(ui.calls[0].options.body.fields.some(f => f[0] === 'Latitude'), false);
    const located = setup(undefined, { getCurrentPosition: callback => callback({ coords: { latitude: 23.8, longitude: 90.4 } }) });
    located.get('use-gps').fire('click'); await located.submit();
    assert.equal(located.calls[0].options.body.fields.find(f => f[0] === 'Latitude')[1], '23.8');
});
test('GPS never replaces the required manual address', async () => {
    const ui = setup(undefined, { getCurrentPosition: callback => callback({ coords: { latitude: 23.8, longitude: 90.4 } }) });
    ui.get('use-gps').fire('click'); ui.get('AddressText').value = ''; await ui.submit(); assert.equal(ui.calls.length, 0);
});
test('duplicate warning accompanies the successful reference', async () => {
    const ui = setup(async () => response(201, { id: 42, referenceNo: 'UA-000042', warning: 'Similar report already submitted.' }));
    await ui.submit(); assert.match(ui.get('status').textContent, /Similar report/); assert.equal(ui.navigations.length, 1);
});
test('429 displays retry guidance, retains values, and waits for user retry after cooldown', async () => {
    const ui = setup(async () => response(429, { retryAfterSeconds: 2 }, '2'));
    await ui.submit(); assert.equal(ui.get('submit').disabled, true); assert.match(ui.get('error').textContent, /2 seconds/);
    assert.equal(ui.get('AddressText').value, 'Dhaka street'); await ui.submit(); assert.equal(ui.calls.length, 1);
    ui.advance(2000); assert.equal(ui.get('submit').disabled, false); assert.equal(ui.calls.length, 1);
});
test('server field errors are rendered as text and keep input intact', async () => {
    const ui = setup(async () => response(400, { errors: { 'request.Photos[0]': ['<b>invalid photo</b>'], AreaId: ['Area no longer exists.'] } }));
    await ui.submit(); assert.equal(ui.get('Photos-error').textContent, '<b>invalid photo</b>');
    assert.equal(ui.get('AreaId').attributes['aria-invalid'], 'true'); assert.equal(ui.get('Description').value, 'Gas leaking near the road');
});
for (const status of [401, 403, 413, 500]) test('handles HTTP ' + status + ' without redirecting or clearing values', async () => {
    const ui = setup(async () => response(status, null)); await ui.submit();
    assert.equal(ui.navigations.length, 0); assert.equal(ui.get('error').hidden, false); assert.equal(ui.get('submit').disabled, false);
    if (status === 401) assert.equal(ui.get('login').hidden, false);
    if (status === 500) assert.equal(ui.get('check-mine').hidden, false);
});
test('network failure or malformed success offers My Reports without automatically resubmitting', async () => {
    for (const fetcher of [async () => { throw new Error('offline'); }, async () => response(201, { id: '../evil', referenceNo: 'bad' })]) {
        const ui = setup(fetcher); await ui.submit(); assert.equal(ui.calls.length, 1); assert.equal(ui.navigations.length, 0);
        assert.equal(ui.get('check-mine').hidden, false);
    }
});
