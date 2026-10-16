const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const script = readFileSync(join(__dirname, '../wwwroot/js/official-report-detail.js'), 'utf8');

function setup({ present = true, complete = false, naturalWidth = 0 } = {}) {
    const events = new Map();
    const photo = present ? { complete, naturalWidth, addEventListener: (name, handler) => events.set(name, handler) } : null;
    const preview = { hidden: false }; const unavailable = { hidden: true };
    runInNewContext(script, { document: { getElementById: id => ({
        'official-report-photo': photo, 'official-photo-preview': preview, 'official-photo-unavailable': unavailable
    })[id] } });
    return { preview, unavailable, error: () => events.get('error')?.() };
}

test('failed report photo shows a fallback instead of a broken preview', () => {
    const s = setup(); s.error();
    assert.equal(s.preview.hidden, true); assert.equal(s.unavailable.hidden, false);
});
test('already failed cached photo shows the fallback immediately', () => {
    const s = setup({ complete: true });
    assert.equal(s.preview.hidden, true); assert.equal(s.unavailable.hidden, false);
});
test('loaded photos remain visible and reports without photos require no script work', () => {
    const s = setup({ complete: true, naturalWidth: 400 });
    assert.equal(s.preview.hidden, false); assert.equal(s.unavailable.hidden, true);
    assert.doesNotThrow(() => setup({ present: false }));
});
