function buildSettingsPayload(enabled) { return { enabled: !!enabled }; }
function mountNotificationSettings({ document = globalThis.document, fetcher = (...args) => globalThis.fetch(...args) } = {}) {
    const get = id => document.getElementById(id), save = get('save-settings'); if (!save) return;
    const checkbox = get('notifications-enabled'), status = get('settings-status');
    let pending = false; save.disabled = true;
    async function load() {
        try {
            const response = await fetcher('/api/resident/areas', { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) throw new Error('Alert settings could not load. Log in or refresh to retry.');
            const data = await response.json();
            if (typeof data.globalNotificationsEnabled !== 'boolean') throw new Error('Invalid alert settings response.');
            checkbox.checked = data.globalNotificationsEnabled; save.disabled = false;
        } catch (error) { status.textContent = error.message; }
    }
    save.addEventListener('click', async () => {
        if (pending || save.disabled) return;
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) { status.textContent = 'Security token missing. Refresh this page.'; return; }
        pending = true; save.disabled = true;
        try {
            const response = await fetcher('/api/resident/areas/global-preference', { method: 'PUT', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', RequestVerificationToken: token }, body: JSON.stringify(buildSettingsPayload(checkbox.checked)) });
            if (!response.ok) throw new Error('Alert preference was not confirmed. Refresh before retrying.');
            status.textContent = 'Alert preference saved. Saved areas are unchanged. Device push must also be enabled for delivery.';
        } catch (error) { status.textContent = error.message; }
        finally { pending = false; save.disabled = false; }
    });
    return { ready: load() };
}
if (typeof document !== 'undefined') mountNotificationSettings();
if (typeof module !== 'undefined') module.exports = { buildSettingsPayload, mountNotificationSettings };
