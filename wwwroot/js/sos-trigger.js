(() => {
    const form = document.getElementById('sos-trigger-form');
    if (!form) return;
    const byId = id => document.getElementById(id);
    const hold = byId('sos-hold');
    const progress = byId('sos-hold-progress');
    const holdStatus = byId('sos-hold-status');
    const error = byId('sos-trigger-error');
    const dialog = byId('sos-review-dialog');
    const confirm = byId('sos-confirm');
    const back = byId('sos-review-back');
    const gps = byId('sos-use-gps');
    const clearGps = byId('sos-clear-gps');
    const retry = byId('sos-retry');
    const submission = new SosSubmission({ onRetry(attempt, delay) {
        byId('sos-send-status').textContent = `Checking for a saved alert in ${delay / 1000} seconds (attempt ${attempt} of 3)…`;
    } });
    let leaving = false;
    let coordinates = null;
    let payload = null;
    let timer;
    let holdStart = 0;
    let held = false;
    let ready = false;
    let pointer = null;
    let key = null;
    let sending = false;
    let gpsGeneration = 0;
    const showError = text => { error.textContent = text; error.focus(); };

    function review() {
        error.textContent = '';
        const addressText = byId('sos-trigger-address').value.trim();
        if ((!coordinates && !addressText) || addressText.length > 500) {
            showError('Use GPS or enter an address of up to 500 characters.'); return;
        }
        if (typeof dialog.showModal !== 'function') {
            showError('This browser cannot show the SOS confirmation. Open this page in a current browser.'); return;
        }
        gpsGeneration++; // Late GPS callbacks cannot change the reviewed location.
        gps.disabled = false;
        payload = { ...(coordinates || {}), addressText };
        byId('sos-review-location').textContent = [
            coordinates ? `GPS ${coordinates.latitude.toFixed(5)}, ${coordinates.longitude.toFixed(5)}` : '', addressText
        ].filter(Boolean).join('\n');
        byId('sos-send-error').textContent = '';
        byId('sos-send-status').textContent = '';
        byId('sos-check-active').hidden = true;
        byId('sos-login').hidden = true;
        retry.hidden = true;
        confirm.disabled = false;
        dialog.showModal();
        back.focus(); // Opening the review never sends the alert.
    }

    function resetHold() {
        clearInterval(timer);
        held = false; ready = false;
        pointer = null; key = null;
        progress.value = 0;
    }
    function beginHold() {
        if (held || sending || dialog.open) return;
        held = true; ready = false; holdStart = Date.now();
        holdStatus.textContent = 'Keep holding to prepare your SOS.';
        timer = setInterval(() => {
            progress.value = Math.min(100, (Date.now() - holdStart) / 20);
            if (progress.value >= 100) {
                ready = true; clearInterval(timer);
                holdStatus.textContent = 'Release to review your SOS.';
            }
        }, 50);
    }
    function releaseHold(cancelled = false) {
        const shouldReview = held && (ready || Date.now() - holdStart >= 2000) && !cancelled;
        resetHold();
        holdStatus.textContent = shouldReview ? 'Review your alert before sending.' : 'Hold released. No SOS alert was sent.';
        if (shouldReview) review();
    }
    hold.addEventListener('pointerdown', event => {
        if (event.button !== 0 || event.isPrimary === false || held) return;
        event.preventDefault(); hold.focus(); beginHold();
        pointer = event.pointerId;
        hold.setPointerCapture?.(pointer);
    });
    hold.addEventListener('pointerup', event => {
        if (event.pointerId !== pointer) return;
        event.preventDefault(); releaseHold();
    });
    hold.addEventListener('pointercancel', () => releaseHold(true));
    hold.addEventListener('lostpointercapture', () => { if (held) releaseHold(true); });
    hold.addEventListener('keydown', event => {
        if (![' ', 'Enter'].includes(event.key)) return;
        event.preventDefault();
        if (event.repeat || held) return;
        beginHold(); key = event.key;
    });
    hold.addEventListener('keyup', event => {
        if (event.key !== key) return;
        event.preventDefault(); releaseHold();
    });
    hold.addEventListener('blur', () => { if (held) releaseHold(true); });
    window.addEventListener('blur', () => { if (held) releaseHold(true); });
    window.addEventListener('pagehide', () => {
        resetHold(); gpsGeneration++; leaving = true; submission.cancel();
        if (sending) {
            retry.hidden = false;
            byId('sos-check-active').hidden = false;
            byId('sos-send-status').textContent = 'Check and retry to confirm the previous request before sending again.';
        }
    });
    window.addEventListener('pageshow', () => { leaving = false; });
    byId('sos-review').addEventListener('click', review);
    form.addEventListener('submit', event => { event.preventDefault(); });

    gps.addEventListener('click', () => {
        if (!navigator.geolocation) {
            byId('sos-gps-message').textContent = 'GPS is unavailable. Enter a manual address below.'; return;
        }
        const generation = ++gpsGeneration;
        gps.disabled = true;
        byId('sos-gps-message').textContent = 'Getting your location…';
        navigator.geolocation.getCurrentPosition(position => {
            if (generation !== gpsGeneration) return;
            gps.disabled = false;
            const { latitude, longitude } = position.coords;
            if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || Math.abs(latitude) > 90 || Math.abs(longitude) > 180) {
                byId('sos-gps-message').textContent = 'GPS did not return a valid location. Enter a manual address.'; return;
            }
            coordinates = { latitude, longitude };
            clearGps.hidden = false;
            byId('sos-gps-message').textContent = `GPS ready: ${latitude.toFixed(5)}, ${longitude.toFixed(5)}.`;
        }, () => {
            if (generation !== gpsGeneration) return;
            gps.disabled = false;
            byId('sos-gps-message').textContent = 'GPS is unavailable or permission was denied. Enter a manual address below.';
        }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 10000 });
    });
    clearGps.addEventListener('click', () => {
        gpsGeneration++; coordinates = null; gps.disabled = false; clearGps.hidden = true;
        byId('sos-gps-message').textContent = 'Enter your manual address below.';
        byId('sos-trigger-address').focus();
    });

    back.addEventListener('click', () => { if (!sending) dialog.close(); });
    dialog.addEventListener('cancel', event => { if (sending) event.preventDefault(); });
    dialog.addEventListener('close', () => { if (!sending) payload = null; });
    async function send() {
        if (sending || !payload || !dialog.open) return;
        if (navigator.onLine === false) {
            byId('sos-send-error').textContent = 'You are offline. Reconnect, then choose Check and retry.';
            retry.hidden = false; retry.disabled = true; return;
        }
        sending = true; confirm.disabled = true; back.disabled = true; retry.disabled = true;
        dialog.setAttribute('aria-busy', 'true');
        byId('sos-send-error').textContent = '';
        byId('sos-send-status').textContent = 'Saving your SOS alert…';
        try {
            const alert = await submission.submit(payload);
            if (leaving) return;
            window.location.assign(`/sos/${encodeURIComponent(alert.id)}`);
        } catch (failure) {
            if (leaving) return;
            if (failure.status === 409) { window.location.assign('/sos/active'); return; }
            if (failure.status === 401) byId('sos-login').hidden = false;
            byId('sos-send-status').textContent = '';
            byId('sos-send-error').textContent = failure.message;
            byId('sos-check-active').hidden = false;
            retry.hidden = ![undefined, 0, 408, 429].includes(failure.status) && !(failure.status >= 500);
        } finally {
            sending = false; back.disabled = false; retry.disabled = navigator.onLine === false;
            dialog.removeAttribute('aria-busy');
        }
    }
    confirm.addEventListener('click', () => { if (!confirm.disabled) return send(); });
    retry.addEventListener('click', () => { if (!retry.disabled) return send(); });
    window.addEventListener('offline', () => {
        retry.disabled = true;
        byId('sos-send-error').textContent = 'You are offline. Your address remains on this page. Reconnect to check and retry.';
    });
    window.addEventListener('online', () => {
        retry.disabled = sending;
        if (!retry.hidden) byId('sos-send-status').textContent = 'Connection restored. Choose Check and retry to continue.';
    });
})();
