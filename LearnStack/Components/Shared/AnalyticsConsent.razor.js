const STORAGE_KEY = 'learnstack.analytics-consent.v1';
const RETENTION_MS = 180 * 24 * 60 * 60 * 1000;
let projectId;
let started = false;

function readConsent() {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return '';
    let stored;
    try {
        stored = JSON.parse(raw);
    } catch (error) {
        console.warn('Invalid analytics consent preference; consent must be requested again.', error);
        return '';
    }
    return stored && (stored.value === 'granted' || stored.value === 'denied')
        && Number.isFinite(stored.expiresAt) && stored.expiresAt > Date.now()
        && stored.expiresAt <= Date.now() + RETENTION_MS
        ? stored.value : '';
}

function start() {
    if (started) return;
    started = true;
    // Clarity requires this vendor-defined queue before its asynchronous tag loads.
    window.clarity = window.clarity || function () {
        (window.clarity.q = window.clarity.q || []).push(arguments);
    };
    window.clarity('consentv2', { analytics_Storage: 'granted', ad_Storage: 'denied' });
    const script = document.createElement('script');
    script.async = true;
    script.src = `https://www.clarity.ms/tag/${projectId}`;
    script.onerror = () => console.error('Microsoft Clarity could not be loaded.');
    document.head.appendChild(script);
}

function stop() {
    if (!started) return;
    window.clarity('consentv2', { analytics_Storage: 'denied', ad_Storage: 'denied' });
    // Reload also stops cookieless collection: denying cookies alone does not stop Clarity.
    for (const name of ['_clck', '_clsk']) {
        document.cookie = `${name}=; Max-Age=0; path=/`;
        const parts = window.location.hostname.split('.');
        for (let i = 0; i < parts.length - 1; i++) {
            document.cookie = `${name}=; Max-Age=0; path=/; domain=${parts.slice(i).join('.')}`;
        }
    }
    window.location.reload();
}

function onStorage(event) {
    if (event.key === STORAGE_KEY || event.key === null) {
        if (readConsent() !== 'granted') stop();
    }
}

export function initialize(id) {
    if (!/^[a-z0-9]+$/i.test(id)) throw new Error('Invalid Clarity project ID.');
    projectId = id;
    const consent = readConsent();
    if (consent === 'granted') start();
    window.addEventListener('storage', onStorage);
    return consent;
}

export function setConsent(accepted) {
    const value = accepted ? 'granted' : 'denied';
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ value, expiresAt: Date.now() + RETENTION_MS }));
    if (accepted) start();
    else stop();
}

export function dispose() {
    window.removeEventListener('storage', onStorage);
}
