import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../LearnStack/Components/Shared/AnalyticsConsent.razor.js', import.meta.url), 'utf8');
const key = 'learnstack.analytics-consent.v1';
let moduleNumber = 0;

async function setup(preference) {
    const values = new Map(preference === undefined ? [] : [[key, preference]]);
    const scripts = [];
    const calls = [];
    const cookies = [];
    const listeners = new Map();
    let reloads = 0;
    globalThis.localStorage = {
        getItem: name => values.get(name) ?? null,
        setItem: (name, value) => values.set(name, value)
    };
    globalThis.window = {
        location: { hostname: 'learnstack.cloud', reload: () => reloads++ },
        clarity: (...args) => calls.push(args),
        addEventListener: (name, listener) => listeners.set(name, listener),
        removeEventListener: name => listeners.delete(name)
    };
    globalThis.document = {
        createElement: tag => ({ tag }),
        head: { appendChild: script => scripts.push(script) },
        set cookie(value) { cookies.push(value); }
    };
    const module = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}#${moduleNumber++}`);
    return { module, scripts, calls, cookies, values, listeners, reloads: () => reloads };
}

function preference(value, expiresAt = Date.now() + 86400000) {
    return JSON.stringify({ value, expiresAt });
}

test('no consent or rejection makes no Clarity calls or requests', async () => {
    for (const stored of [undefined, preference('denied'), preference('granted', Date.now() - 1),
        preference('granted', Date.now() + 181 * 86400000), preference('invalid'), 'null', '{}']) {
        const state = await setup(stored);
        await state.module.initialize('z9ncpem9a9');
        assert.equal(state.scripts.length, 0);
        assert.equal(state.calls.length, 0);
        state.module.dispose();
        assert.equal(state.listeners.size, 0);
    }
});

test('acceptance persists bounded consent and loads the supplied tag once, with ads denied', async () => {
    const state = await setup();
    assert.equal(state.module.initialize('z9ncpem9a9'), '');
    state.module.setConsent(true);
    state.module.setConsent(true);
    assert.equal(state.scripts.length, 1);
    assert.equal(state.scripts[0].src, 'https://www.clarity.ms/tag/z9ncpem9a9');
    assert.equal(state.scripts[0].async, true);
    assert.deepEqual(state.calls, [['consentv2', { analytics_Storage: 'granted', ad_Storage: 'denied' }]]);
    const stored = JSON.parse(state.values.get(key));
    assert.equal(stored.value, 'granted');
    assert.ok(stored.expiresAt > Date.now());
    assert.ok(stored.expiresAt <= Date.now() + 180 * 86400000);
});

test('valid saved consent starts analytics on subsequent visits', async () => {
    const state = await setup(preference('granted'));
    assert.equal(state.module.initialize('z9ncpem9a9'), 'granted');
    assert.equal(state.scripts.length, 1);
});

test('rejection is remembered without loading analytics or reloading', async () => {
    const state = await setup();
    state.module.initialize('z9ncpem9a9');
    state.module.setConsent(false);
    assert.equal(JSON.parse(state.values.get(key)).value, 'denied');
    assert.equal(state.scripts.length, 0);
    assert.equal(state.reloads(), 0);
});

test('withdrawal denies both storage categories, clears cookies and reloads to stop collection', async () => {
    const state = await setup(preference('granted'));
    state.module.initialize('z9ncpem9a9');
    state.module.setConsent(false);
    assert.equal(JSON.parse(state.values.get(key)).value, 'denied');
    assert.deepEqual(state.calls.at(-1), ['consentv2', { analytics_Storage: 'denied', ad_Storage: 'denied' }]);
    assert.ok(state.cookies.includes('_clck=; Max-Age=0; path=/'));
    assert.ok(state.cookies.includes('_clsk=; Max-Age=0; path=/; domain=learnstack.cloud'));
    assert.equal(state.reloads(), 1);
});

test('withdrawal or clearing preferences in another tab stops this tab', async () => {
    for (const eventKey of [key, null]) {
        const state = await setup(preference('granted'));
        state.module.initialize('z9ncpem9a9');
        state.values.clear();
        state.listeners.get('storage')({ key: eventKey });
        assert.equal(state.reloads(), 1);
    }
});

test('unavailable preference storage fails closed before requesting Clarity', async () => {
    const state = await setup();
    localStorage.getItem = () => { throw new Error('Storage unavailable'); };
    assert.throws(() => state.module.initialize('z9ncpem9a9'), /Storage unavailable/);
    assert.equal(state.scripts.length, 0);
    localStorage.setItem = () => { throw new Error('Storage unavailable'); };
    assert.throws(() => state.module.setConsent(true), /Storage unavailable/);
    assert.equal(state.scripts.length, 0);
});

test('invalid project IDs fail before any network request', async () => {
    const state = await setup(preference('granted'));
    assert.throws(() => state.module.initialize('../invalid'), /Invalid Clarity project ID/);
    assert.equal(state.scripts.length, 0);
});
