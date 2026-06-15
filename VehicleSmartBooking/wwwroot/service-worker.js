// Vehicle Smart Booking — Service Worker
// Handles Web Push notifications and notification click routing.

self.addEventListener('push', function (event) {
    var data = {};
    if (event.data) {
        try { data = event.data.json(); }
        catch (e) { data = { title: 'Vehicle Smart Booking', body: event.data.text() }; }
    }

    var title = data.title || 'Vehicle Smart Booking';
    var options = {
        body: data.body || '',
        icon: '/img/van.png',
        badge: '/img/van.png',
        data: { url: data.url || '/' },
        requireInteraction: false,
        vibrate: [200, 100, 200]
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', function (event) {
    event.notification.close();

    var targetUrl = (event.notification.data && event.notification.data.url)
        ? event.notification.data.url
        : '/';

    // Ensure the URL is absolute
    if (targetUrl && !targetUrl.startsWith('http')) {
        targetUrl = self.location.origin + targetUrl;
    }

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            for (var i = 0; i < clientList.length; i++) {
                var client = clientList[i];
                if (client.url === targetUrl && 'focus' in client) {
                    return client.focus();
                }
            }
            if (clients.openWindow) {
                return clients.openWindow(targetUrl);
            }
        })
    );
});
