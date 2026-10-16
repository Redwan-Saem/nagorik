(() => {
    const page = document.getElementById('sos-active');
    if (!page) return;
    const start = document.getElementById('start-sos-tracking');
    const stop = document.getElementById('stop-sos-tracking');
    const message = document.getElementById('sos-tracking-message');
    const error = document.getElementById('sos-tracking-error');
    const manualForm = document.getElementById('sos-manual-location');
    let terminal = false;
    let cancelling = false;
    let updatingAddress = false;
    const buttons = () => {
        start.disabled = terminal || cancelling || updatingAddress || tracker.running;
        stop.disabled = !tracker.running;
        manualForm.querySelector('button').disabled = terminal || cancelling || updatingAddress;
    };
    const tracker = new SosLiveLocation({
        onStatus(text) { message.textContent = text; error.textContent = ''; buttons(); },
        onError(text) { error.textContent = text; buttons(); },
        onAlert(alert) {
            document.getElementById('sos-status').textContent = alert.status;
            terminal = !['Active', 'Responding'].includes(alert.status);
            buttons();
        },
        onLocation(point) {
            document.getElementById('sos-location').textContent = `GPS ${point.latitude.toFixed(5)}, ${point.longitude.toFixed(5)}`;
        }
    });
    start.addEventListener('click', async () => {
        start.disabled = true;
        await tracker.start(page.dataset.alertId);
        buttons();
    });
    stop.addEventListener('click', () => {
        tracker.stop(); message.textContent = 'Live location sharing stopped.'; buttons();
    });
    window.addEventListener('pagehide', () => {
        tracker.stop(); buttons(); message.textContent = 'Live location sharing stopped.';
    });
    manualForm.addEventListener('submit', async event => {
        event.preventDefault();
        if (terminal || cancelling || updatingAddress) return;
        const form = event.currentTarget;
        if (!form.reportValidity()) return;
        updatingAddress = true;
        tracker.stop(); buttons();
        const submit = form.querySelector('button');
        submit.disabled = true;
        error.textContent = '';
        try {
            const response = await fetch(`/api/sos/${encodeURIComponent(page.dataset.alertId)}/location`, {
                method: 'PATCH', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                body: JSON.stringify({ addressText: document.getElementById('sos-address').value })
            });
            if (!response.ok) throw new Error('Address could not be updated. Refresh the alert to check its status or log in again.');
            const alert = await response.json();
            document.getElementById('sos-location').textContent = alert.addressText;
            document.getElementById('sos-status').textContent = alert.status;
            message.textContent = 'Manual address saved. Live GPS sharing is stopped.';
        } catch (failure) { error.textContent = failure.message; }
        finally { updatingAddress = false; buttons(); }
    });
})();
