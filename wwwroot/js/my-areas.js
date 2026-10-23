(() => {
    function mount({ document = globalThis.document, fetcher = (...args) => globalThis.fetch(...args),
        navigator = globalThis.navigator, locator = globalThis.ZoneLocator } = {}) {
        const get = id => document.getElementById(id), form = get('area-subscribe'); if (!form) return;
        const select = get('area-zone'), list = get('areas-list'), status = get('areas-status'), save = get('area-save');
        let busy = false, selectionVersion = 0;
        select.addEventListener('change', () => { selectionVersion++; });
        async function load() {
            try {
                const response = await fetcher('/api/resident/areas', { credentials:'same-origin', cache:'no-store' });
                if (!response.ok) throw new Error(response.status === 401 ? 'Session expired. Log in again.' : 'Areas could not load. Refresh the page to retry.');
                const data = await response.json();
                if (!Array.isArray(data.zones) || !Array.isArray(data.subscriptions)) throw new Error('Invalid area response.');
                const selected = select.value; select.replaceChildren();
                const placeholder = document.createElement('option'); placeholder.value = ''; placeholder.textContent = 'Choose a zone'; select.appendChild(placeholder);
                for (const zone of data.zones) { const option = document.createElement('option'); option.value = String(zone.id); option.textContent = `${zone.name} (${zone.code})`; select.appendChild(option); }
                select.value = selected || ''; list.replaceChildren();
                for (const subscription of data.subscriptions) {
                    const zone = data.zones.find(z => z.id === subscription.zoneId), item = document.createElement('li');
                    const label = document.createElement('span'); label.textContent = `${zone?.name || 'Zone'} — alerts ${subscription.alertsEnabled ? 'on' : 'off'} `; item.appendChild(label);
                    const toggle = document.createElement('button'); toggle.type = 'button'; toggle.dataset.areaToggle = subscription.zoneId;
                    toggle.textContent = subscription.alertsEnabled ? 'Turn alerts off' : 'Turn alerts on';
                    toggle.addEventListener('click', () => write(subscription.zoneId,'PUT',!subscription.alertsEnabled)); item.appendChild(toggle);
                    const remove = document.createElement('button'); remove.type = 'button'; remove.dataset.areaRemove = subscription.zoneId; remove.textContent = 'Remove area';
                    remove.addEventListener('click', () => write(subscription.zoneId,'DELETE')); item.appendChild(remove); list.appendChild(item);
                }
                if (!data.subscriptions.length) { const empty = document.createElement('li'); empty.textContent = 'No saved areas yet.'; list.appendChild(empty); }
                get('areas-global-status').textContent = data.globalNotificationsEnabled ? 'Global notifications are enabled. Device push permission is also required for delivery.' :
                    'Global notifications are OFF. Your areas can be saved, but alerts will not be delivered until you enable global alerts.';
                return true;
            } catch (error) { status.textContent = error.message || 'Areas could not load.'; return false; }
        }
        async function write(zoneId, method, enabled) {
            if (busy) return; if (!Number.isInteger(Number(zoneId)) || Number(zoneId) <= 0) { status.textContent = 'Choose a zone first.'; return; }
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (!token) { status.textContent = 'Security token missing. Refresh this page.'; return; }
            busy = true; save.disabled = true; status.textContent = 'Saving area…';
            try {
                const response = await fetcher(`/api/resident/areas/${Number(zoneId)}`, { method, credentials:'same-origin',
                    headers:{'Content-Type':'application/json',RequestVerificationToken:token}, ...(method === 'PUT' ? {body:JSON.stringify({alertsEnabled:enabled})} : {}) });
                if (!response.ok) throw new Error(response.status === 401 ? 'Session expired. Log in again.' : 'Area update was not confirmed. Refresh before retrying.');
                if (await load()) status.textContent = method === 'DELETE' ? 'Area removed.' : 'Area saved.';
            } catch (error) { status.textContent = error.message; }
            finally { busy = false; save.disabled = false; }
        }
        form.addEventListener('submit', event => { event.preventDefault(); return write(Number(select.value),'PUT',get('area-alerts').checked); });
        get('area-locate').addEventListener('click', () => {
            if (!navigator?.geolocation) { status.textContent = 'GPS unavailable. Choose a zone manually.'; return; }
            const revision = selectionVersion; status.textContent = 'Requesting location to suggest an area…';
            try { navigator.geolocation.getCurrentPosition(async position => {
                try {
                    const response = await fetcher('/api/public/map/risk-zones', {credentials:'omit',cache:'no-store'});
                    if (!response.ok) throw new Error('Zone boundaries unavailable. Choose manually.');
                    const data = await response.json();
                    const match = data.features.find(f => locator.contains(f.geometry,position.coords.latitude,position.coords.longitude));
                    if (revision !== selectionVersion) return;
                    if (match) { select.value = String(match.id); status.textContent = `Suggested ${match.properties.name}. Review your choice and choose Add or update area to save it.`; }
                    else status.textContent = 'Your location is outside the available zones. Choose an area manually.';
                } catch (error) { status.textContent = error.message; }
            }, () => { status.textContent = 'GPS denied or unavailable. Choose a zone manually.'; }, {enableHighAccuracy:true,timeout:15000,maximumAge:30000}); }
            catch { status.textContent = 'GPS unavailable. Choose a zone manually.'; }
        });
        return { ready:load(), reload:load, write };
    }
    globalThis.MyAreas = { mount };
    if (typeof document !== 'undefined') mount();
})();
