(() => {
    const page = document.getElementById('sos-active');
    if (!page) return;
    const start = document.getElementById('start-sos-tracking');
    const stop = document.getElementById('stop-sos-tracking');
    const message = document.getElementById('sos-tracking-message');
    const error = document.getElementById('sos-tracking-error');
    const cancel = document.getElementById('sos-cancel');
    const cancelDialog = document.getElementById('sos-cancel-dialog');
    const cancelConfirm = document.getElementById('sos-cancel-confirm');
    const cancelBack = document.getElementById('sos-cancel-back');
    const manualForm = document.getElementById('sos-manual-location');
    let terminal = false;
    let cancelling = false;
    let updatingAddress = false;
    const buttons = () => {
        start.disabled = terminal || cancelling || updatingAddress || tracker.running;
        stop.disabled = !tracker.running;
        cancel.disabled = terminal || cancelling || updatingAddress;
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
    cancel.addEventListener('click', () => {
        if (terminal || cancelling || updatingAddress) return;
        document.getElementById('sos-cancel-error').textContent = '';
        cancelConfirm.disabled = false;
        cancelDialog.showModal(); cancelBack.focus();
    });
    cancelBack.addEventListener('click', () => { if (!cancelling) cancelDialog.close(); });
    cancelDialog.addEventListener('cancel', event => { if (cancelling) event.preventDefault(); });
    cancelConfirm.addEventListener('click', async () => {
        if (cancelling || terminal || !cancelDialog.open || cancelConfirm.disabled) return;
        cancelling = true; cancelConfirm.disabled = true; cancelBack.disabled = true;
        tracker.stop(); buttons();
        cancelDialog.setAttribute('aria-busy', 'true');
        document.getElementById('sos-cancel-message').textContent = 'Cancelling your SOS alert…';
        try {
            const response = await fetch(`/api/sos/${encodeURIComponent(page.dataset.alertId)}/cancel`, {
                method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
            });
            if (!response.ok) throw new Error('Cancellation could not be confirmed. Check the latest alert status or log in again.');
            const alert = await response.json();
            if (alert.status !== 'Cancelled') throw new Error('Check the latest alert status to confirm cancellation.');
            terminal = true;
            window.location.assign(`/sos/${encodeURIComponent(page.dataset.alertId)}`);
        } catch (failure) {
            document.getElementById('sos-cancel-message').textContent = '';
            document.getElementById('sos-cancel-error').textContent = failure.message;
        } finally {
            cancelling = false; cancelBack.disabled = false; cancelDialog.removeAttribute('aria-busy'); buttons();
        }
    });
})();
