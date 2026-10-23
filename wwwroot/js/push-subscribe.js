function urlBase64ToUint8Array(base64) {
    const padding = '='.repeat((4 - base64.length % 4) % 4);
    const raw = atob((base64 + padding).replace(/-/g, '+').replace(/_/g, '/'));
    return Uint8Array.from(raw, character => character.charCodeAt(0));
}
async function enablePush({ navigator = globalThis.navigator, Notification = globalThis.Notification,
    PushManager = globalThis.PushManager, fetcher = (...args) => globalThis.fetch(...args) } = {}) {
    if (!navigator?.serviceWorker || !PushManager || !Notification) return { ok: false, message: 'Push is not supported in this browser.' };
    try {
        const permission = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission();
        if (permission !== 'granted') return { ok: false, message: 'Notification permission was not granted.' };
        await navigator.serviceWorker.register('/sw.js');
        const registration = await navigator.serviceWorker.ready;
        const response = await fetcher('/api/push/public-key', { credentials: 'same-origin' });
        if (!response.ok) throw new Error('Push configuration unavailable');
        const { key } = await response.json();
        if (typeof key !== 'string' || !key.trim()) throw new Error('Push key unavailable');
        let subscription = await registration.pushManager.getSubscription();
        if (!subscription) subscription = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(key) });
        const data = subscription.toJSON();
        const saved = await fetcher('/api/push/subscribe', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ endpoint: data.endpoint, p256dh: data.keys.p256dh, auth: data.keys.auth }) });
        return { ok: saved.ok, message: saved.ok ? 'Notifications enabled on this device.' : 'Could not save this device subscription. Try again.' };
    } catch { return { ok: false, message: 'Push setup failed. Check browser permission, connectivity and the server push configuration.' }; }
}
if (typeof document !== 'undefined') {
    document.addEventListener('DOMContentLoaded', () => {
        const button = document.getElementById('enable-push'); if (!button) return;
        button.addEventListener('click', async () => {
            if (button.disabled) return; button.disabled = true;
            try { const result = await enablePush(); const status = document.getElementById('push-status');
                if (status) status.textContent = result.message; else if (typeof alert === 'function') alert(result.message);
            } finally { button.disabled = false; }
        });
    });
}
if (typeof module !== 'undefined') module.exports = { enablePush, urlBase64ToUint8Array };
