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