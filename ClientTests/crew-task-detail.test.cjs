const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/crew-task-detail.js'), 'utf8');

function setup(fetch, { completed = false, success = false } = {}) {
    const elements = Object.fromEntries(['task-feedback', 'task-error', 'task-login', 'update-task-status'].map(id => [id, {
        hidden: true, textContent: '', disabled: false, dataset: { nextStatus: 'InProgress' },
        focus() { this.focused = true; }, addEventListener(event, handler) { this.click = handler; }
    }]));
    const task = {
        dataset: { taskId: '12' }, attributes: {},
        setAttribute(name, value) { this.attributes[name] = value; },
        removeAttribute(name) { delete this.attributes[name]; }
    };
    const storage = new Map(success ? [['crew-task-updated-12', '1']] : []);
    let reloads = 0;
    runInNewContext(script, {
        document: {
            querySelector() { return task; },
            getElementById(id) { return completed && id === 'update-task-status' ? null : elements[id]; }
        }, fetch,
        sessionStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
        window: { location: { pathname: '/crew/tasks/12', reload() { reloads++; } } }
    });
    return { elements, task, storage, get reloads() { return reloads; } };
}

test('submits the next status with cookies, prevents double clicks, then refreshes', async () => {
    let resolve;
    let calls = 0;
    const state = setup((url, options) => {
        calls++;
        assert.equal(url, '/api/crew/tasks/12/status');
        assert.equal(options.method, 'PATCH');
        assert.equal(options.credentials, 'same-origin');
        assert.deepEqual(JSON.parse(options.body), { status: 'InProgress' });
        return new Promise(done => { resolve = done; });
    });
    const button = state.elements['update-task-status'];
    const pending = button.click();
    assert.equal(button.disabled, true);
    assert.equal(state.task.attributes['aria-busy'], 'true');
    assert.match(state.elements['task-feedback'].textContent, /Updating/);
    await button.click();
    assert.equal(calls, 1);
    resolve({ ok: true });
    await pending;
    assert.equal(state.reloads, 1);
    assert.equal(state.storage.get('crew-task-updated-12'), '1');
    assert.equal(state.task.attributes['aria-busy'], undefined);
});

for (const [status, message] of [[401, /session has expired/], [403, /permission/], [404, /no longer available/], [409, /Refresh the task/], [500, /could not be confirmed/]]) {
    test(`handles HTTP ${status} without resubmitting a potentially stale command`, async () => {
        const state = setup(async () => ({ ok: false, status }));
        await state.elements['update-task-status'].click();
        assert.match(state.elements['task-error'].textContent, message);
        assert.equal(state.elements['task-error'].hidden, false);
        assert.equal(state.elements['task-error'].focused, true);
        assert.equal(state.elements['update-task-status'].disabled, true);
        assert.equal(state.reloads, 0);
        if (status === 401) {
            assert.equal(state.elements['task-login'].hidden, false);
            assert.equal(state.elements['task-login'].href, '/login.html?returnUrl=%2Fcrew%2Ftasks%2F12');
        }
    });
}

test('connection failure asks for a refresh before retrying', async () => {
    const state = setup(async () => { throw new Error('offline'); });
    await state.elements['update-task-status'].click();
    assert.match(state.elements['task-error'].textContent, /Check your connection/);
    assert.equal(state.elements['update-task-status'].disabled, true);
    assert.equal(state.task.attributes['aria-busy'], undefined);
});

test('shows success after refresh even when the task has no further action', () => {
    const state = setup(() => assert.fail('completed task must not submit'), { completed: true, success: true });
    assert.match(state.elements['task-feedback'].textContent, /updated successfully/);
    assert.equal(state.elements['task-feedback'].focused, true);
    assert.equal(state.storage.size, 0);
});
