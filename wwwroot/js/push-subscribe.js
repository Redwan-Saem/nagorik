function urlBase64ToUint8Array(base64) {
    const padding = '='.repeat((4 - base64.length % 4) % 4);
    const b64 = (base64 + padding)
        .replace(/-/g, '+')
        .replace(/_/g, '/');

    const raw = atob(b64);
    const out = new Uint8Array(raw.length);

    for (let i = 0; i < raw.length; i++) {
        out[i] = raw.charCodeAt(i);
    }

    return out;
}

async function enablePush() {
    if (!('serviceWorker' in navigator) ||
        !('PushManager' in window)) {
        return {
            ok: false,
            message: 'Push is not supported in this browser.'
        };
    }

    const perm = await Notification.requestPermission();

    if (perm !== 'granted') {
        return {
            ok: false,
            message: 'Notification permission was not granted.'
        };
    }

    const reg = await navigator.serviceWorker.register('/sw.js');

    await navigator.serviceWorker.ready;

    const { key } =
        await (await fetch('/api/push/public-key')).json();

    const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(key)
    });

    const j = sub.toJSON();

    const r = await fetch('/api/push/subscribe', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            endpoint: j.endpoint,
            p256dh: j.keys.p256dh,
            auth: j.keys.auth
        })
    });

    return {
        ok: r.ok,
        message: r.ok
            ? 'Notifications enabled on this device.'
            : 'Could not save subscription.'
    };
}

if (typeof document !== 'undefined') {
    document.addEventListener('DOMContentLoaded', () => {
        const b = document.getElementById('enable-push');

        if (b) {
            b.addEventListener('click', async () => {
                const r = await enablePush();
                alert(r.message);
            });
        }
    });""
}function urlBase64ToUint8Array(base64) {
    const padding = '='.repeat((4 - base64.length % 4) % 4);
    const b64 = (base64 + padding)
        .replace(/-/g, '+')
        .replace(/_/g, '/');

    const raw = atob(b64);
    const out = new Uint8Array(raw.length);

    for (let i = 0; i < raw.length; i++) {
        out[i] = raw.charCodeAt(i);
    }

    return out;
}

async function enablePush() {
    if (!('serviceWorker' in navigator) ||
        !('PushManager' in window)) {
        return {
            ok: false,
            message: 'Push is not supported in this browser.'
        };
    }

    const perm = await Notification.requestPermission();

    if (perm !== 'granted') {
        return {
            ok: false,
            message: 'Notification permission was not granted.'
        };
    }

    const reg = await navigator.serviceWorker.register('/sw.js');

    await navigator.serviceWorker.ready;

    const { key } =
        await (await fetch('/api/push/public-key')).json();

    const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(key)
    });

    const j = sub.toJSON();

    const r = await fetch('/api/push/subscribe', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            endpoint: j.endpoint,
            p256dh: j.keys.p256dh,
            auth: j.keys.auth
        })
    });

    return {
        ok: r.ok,
        message: r.ok
            ? 'Notifications enabled on this device.'
            : 'Could not save subscription.'
    };
}

if (typeof document !== 'undefined') {
    document.addEventListener('DOMContentLoaded', () => {
        const b = document.getElementById('enable-push');

        if (b) {
            b.addEventListener('click', async () => {
                const r = await enablePush();
                alert(r.message);
            });
        }
    });
}

if (typeof module !== 'undefined') {
    module.exports = {
        urlBase64ToUint8Array
    };
}

if (typeof module !== 'undefined') {
    module.exports = {
        urlBase64ToUint8Array
    };
}