const scheduleCacheName = 'nagorik-resident-schedule-shell-v1';
const scheduleAssets = [
    '/resident-loadshedding-offline.html', '/css/site.css', '/css/loadshedding.css',
    '/js/zone-locator.js', '/js/resident-schedule-location.js',
    '/js/resident-schedule-transport.js', '/js/resident-loadshedding.js'
];
self.addEventListener('install', event => {
    event.waitUntil(caches.open(scheduleCacheName).then(cache => cache.addAll(scheduleAssets)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', event => {
    event.waitUntil(self.clients.claim());
});
self.addEventListener('fetch', event => {
    const request = event.request, url = new URL(request.url);
    if (request.method !== 'GET' || url.origin !== self.location.origin) return;
    if (request.mode === 'navigate' && url.pathname.replace(/\/$/, '') === '/resident/loadshedding') {
        // Do not cache authenticated HTML, cookies, tokens or API responses.
        // Only a generic static shell is returned on a real network failure.
        // A real HTTP 401/403 response always reaches the page unchanged.
        event.respondWith(fetch(request).catch(() => caches.open(scheduleCacheName).then(cache => cache.match('/resident-loadshedding-offline.html'))));
    } else if (scheduleAssets.includes(url.pathname)) {
        event.respondWith(fetch(request).then(response => {
            if (response.ok) {
                const copy = response.clone();
                event.waitUntil(caches.open(scheduleCacheName).then(cache => cache.put(url.pathname, copy)).catch(() => {}));
            }
            return response;
        }).catch(() => caches.open(scheduleCacheName).then(cache => cache.match(url.pathname))));
    }
});

self.addEventListener('push', event => {
    const data = event.data
        ? event.data.json()
        : {
            title: 'Nagorik',
            body: '',
            url: '/'
        };

    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            data: {
                url: data.url
            }
        })
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();

    const url = event.notification.data.url || '/';

    event.waitUntil(
        clients.matchAll({
            type: 'window',
            includeUncontrolled: true
        }).then(list => {
            for (const client of list) {
                if (client.url.includes(url) && 'focus' in client) {
                    return client.focus();
                }
            }

            return clients.openWindow(url);
        })
    );
});
