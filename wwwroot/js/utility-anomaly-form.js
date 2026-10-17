(() => {
    'use strict';
    const get = id => document.getElementById('utility-' + id);
    const form = get('form');
    if (!form) return;
    const names = ['AnomalyType', 'AreaId', 'AddressText', 'Description', 'Photos', 'Latitude', 'Longitude'];
    const riskTypes = form.dataset.highRiskTypes.split(',');
    let busy = false, checking = false, locating = false, gps = null, gpsVersion = 0, photoVersion = 0;
    let previewUrls = [], cooldownUntil = 0, cooldownTimer = null;
    function sync() {
        get('submit').disabled = busy || checking || locating || Date.now() < cooldownUntil;
        get('fields').disabled = busy;
        form.setAttribute('aria-busy', String(busy || checking));
    }
    function fieldError(name, message) {
        const error = get(name + '-error');
        error.textContent = message || ''; error.hidden = !message;
        get(name)?.setAttribute('aria-invalid', String(!!message));
    }
    function message(text) {
        get('error').textContent = text; get('error').hidden = !text;
    }
    function clearErrors() { names.forEach(name => fieldError(name, '')); message(''); }
    function clearPreviews() {
        previewUrls.forEach(url => URL.revokeObjectURL(url)); previewUrls = [];
        get('previews').replaceChildren();
    }
    async function validatePhotos(files) {
        if (files.length < 1 || files.length > 3) return 'Choose one to three photos.';
        for (const file of files) {
            if (file.size <= 0 || file.size > 5 * 1024 * 1024) return 'Each photo must contain data and be at most 5 MiB.';
            const jpeg = /\.jpe?g$/i.test(file.name), png = /\.png$/i.test(file.name);
            if ((!jpeg && !png) || file.type !== (jpeg ? 'image/jpeg' : 'image/png')) return 'Choose JPG or PNG photos with matching file types.';
            const header = new Uint8Array(await file.slice(0, 8).arrayBuffer());
            if (jpeg ? header.length < 3 || header[0] !== 255 || header[1] !== 216 || header[2] !== 255
                : header.length < 8 || ![137, 80, 78, 71, 13, 10, 26, 10].every((byte, i) => header[i] === byte))
                return 'A selected file does not contain a valid JPG or PNG signature.';
        }
        return '';
    }
    get('Photos').addEventListener('change', async () => {
        const version = ++photoVersion, files = Array.from(get('Photos').files || []);
        clearPreviews(); checking = true; sync();
        get('clear-photos').hidden = files.length === 0;
        try {
            const error = await validatePhotos(files);
            if (version !== photoVersion) return;
            fieldError('Photos', error);
            if (!error) files.forEach(file => {
                const figure = document.createElement('figure'), image = document.createElement('img'), caption = document.createElement('figcaption');
                const url = URL.createObjectURL(file); previewUrls.push(url);
                image.src = url; image.alt = 'Selected photo: ' + file.name;
                caption.textContent = file.name; figure.appendChild(image); figure.appendChild(caption); get('previews').appendChild(figure);
            });
        } catch {
            if (version === photoVersion) fieldError('Photos', 'Could not read these photos. Choose the files again.');
        } finally { if (version === photoVersion) { checking = false; sync(); } }
    });
    get('clear-photos').addEventListener('click', () => {
        ++photoVersion; checking = false; get('Photos').value = ''; clearPreviews();
        fieldError('Photos', ''); get('clear-photos').hidden = true; sync();
    });
    get('AnomalyType').addEventListener('change', () => {
        get('safety').hidden = !riskTypes.includes(get('AnomalyType').value);
        fieldError('AnomalyType', '');
    });
    get('Description').addEventListener('input', () => {
        get('count').textContent = get('Description').value.length + ' / 1000 characters';
        fieldError('Description', '');
    });
    ['AreaId', 'AddressText'].forEach(name => get(name).addEventListener('input', () => fieldError(name, '')));
    function clearGps() {
        ++gpsVersion; gps = null; locating = false; get('use-gps').disabled = false;
        get('clear-gps').hidden = true; get('gps-status').textContent = 'Using your address only. GPS is optional.'; sync();
    }
    get('clear-gps').addEventListener('click', clearGps);
    get('use-gps').addEventListener('click', () => {
        const version = ++gpsVersion; gps = null;
        if (!navigator.geolocation) { get('gps-status').textContent = 'GPS is unavailable. Enter your address and landmark instead.'; return; }
        locating = true; get('use-gps').disabled = true; get('clear-gps').hidden = false;
        get('gps-status').textContent = 'Finding your location. You can choose address only to continue without GPS.'; sync();
        try { navigator.geolocation.getCurrentPosition(position => {
            if (version !== gpsVersion) return;
            const { latitude, longitude } = position.coords;
            locating = false; get('use-gps').disabled = false;
            if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180) {
                gps = null; get('gps-status').textContent = 'GPS returned an invalid location. Use your address instead.';
            } else {
                gps = { latitude, longitude };
                get('gps-status').textContent = 'GPS added: ' + latitude.toFixed(5) + ', ' + longitude.toFixed(5) + '. An address is still required.';
            }
            sync();
        }, () => {
            if (version !== gpsVersion) return;
            clearGps(); get('gps-status').textContent = 'GPS was denied or unavailable. You can still submit using your address.';
        }, { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 }); }
        catch { clearGps(); get('gps-status').textContent = 'GPS is unavailable. You can still submit using your address.'; }
    });
    function rateLimit(response, body) {
        const header = response.headers.get('Retry-After');
        const value = header && /^\d+$/.test(header) ? Number(header) : Number(body?.retryAfterSeconds);
        const seconds = Number.isFinite(value) && value > 0 ? Math.min(Math.ceil(value), 3600) : 60;
        cooldownUntil = Date.now() + seconds * 1000;
        if (cooldownTimer) clearInterval(cooldownTimer);
        const update = () => {
            const remaining = Math.max(0, Math.ceil((cooldownUntil - Date.now()) / 1000));
            message(remaining ? 'You have reached the limit of 10 utility reports per hour. Try again in ' + remaining + ' seconds.'
                : 'You can now try submitting again. Your details have been kept.');
            if (!remaining) { clearInterval(cooldownTimer); cooldownTimer = null; }
            sync();
        };
        update(); cooldownTimer = setInterval(update, 1000);
    }
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy || checking || locating || Date.now() < cooldownUntil) return;
        busy = true; sync(); clearErrors(); get('status').textContent = '';
        get('check-mine').hidden = true; get('login').hidden = true;
        let navigated = false;
        try {
            const files = Array.from(get('Photos').files || []);
            const values = { AnomalyType: get('AnomalyType').value, AreaId: get('AreaId').value,
                AddressText: get('AddressText').value.trim(), Description: get('Description').value.trim() };
            const errors = {};
            if (!values.AnomalyType) errors.AnomalyType = 'Choose a problem type.';
            if (!values.AreaId) errors.AreaId = 'Choose an area.';
            if (!values.AddressText || values.AddressText.length > 300) errors.AddressText = 'Enter an address or landmark, up to 300 characters.';
            if (values.Description.length < 10 || values.Description.length > 1000) errors.Description = 'Use 10–1000 characters for the description.';
            const photoError = await validatePhotos(files); if (photoError) errors.Photos = photoError;
            if (Object.keys(errors).length) {
                Object.entries(errors).forEach(([name, error]) => fieldError(name, error));
                message('Please correct the highlighted fields.'); busy = false; sync(); get(Object.keys(errors)[0]).focus(); return;
            }
            const body = new FormData(); Object.entries(values).forEach(([key, value]) => body.append(key, value));
            files.forEach(file => body.append('Photos', file, file.name));
            if (gps) { body.append('Latitude', String(gps.latitude)); body.append('Longitude', String(gps.longitude)); }
            get('status').textContent = 'Submitting your report…';
            const response = await fetch('/api/reports/utility-anomalies', { method: 'POST', credentials: 'same-origin', body });
            const result = await response.json().catch(() => null);
            if (response.status === 201 && Number.isInteger(result?.id) && result.id > 0 && /^UA-\d{6,}$/.test(result.referenceNo)) {
                get('status').textContent = 'Submitted. Reference: ' + result.referenceNo + (result.warning ? '. ' + result.warning : '');
                window.location.assign('/Reports/Confirmation/' + encodeURIComponent(result.id)); navigated = true; return;
            }
            get('status').textContent = '';
            if (response.status === 429) { rateLimit(response, result); return; }
            if (response.status === 401) { message('Your session has expired. Sign in again to submit.'); get('login').hidden = false; return; }
            if (response.status === 403) { message('Only resident accounts can submit utility reports.'); return; }
            if (response.status === 413) { fieldError('Photos', 'The upload is too large. Choose up to three photos of at most 5 MiB each.'); message('Please reduce the selected photos.'); return; }
            if (response.status === 400) {
                for (const [key, messages] of Object.entries(result?.errors || {})) {
                    const name = key.split('.').pop().replace(/\[.*$/, '');
                    const known = names.find(field => field.toLowerCase() === name.toLowerCase());
                    if (known) fieldError(known, Array.isArray(messages) ? messages.join(' ') : String(messages));
                }
                message('Please check the highlighted fields and try again.'); get('error').focus(); return;
            }
            message('We could not confirm submission. Check My Reports before trying again.'); get('check-mine').hidden = false;
        } catch {
            get('status').textContent = ''; message('We could not confirm submission. Check My Reports before trying again.'); get('check-mine').hidden = false;
        } finally { if (!navigated) { busy = false; sync(); } }
    });
    window.addEventListener('pagehide', () => { clearPreviews(); if (cooldownTimer) clearInterval(cooldownTimer); });
    window.addEventListener('pageshow', event => { if (event.persisted) { busy = false; clearGps(); sync(); } });
    sync();
})();
