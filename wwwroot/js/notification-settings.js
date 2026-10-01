function buildSettingsPayload(enabled, checkedIds) {
    return {
        enabled: !!enabled,
        zoneIds: checkedIds.map(Number)
    };
}

if (typeof document !== 'undefined') {
    document.addEventListener('DOMContentLoaded', async () => {
        const $ = id => document.getElementById(id);

        const s = await (
            await fetch('/api/notification-settings')
        ).json();

        $('notifications-enabled').checked = s.enabled;

        $('zone-list').innerHTML = s.zones.map(z =>
            `<div class="form-check">
                <input class="form-check-input zone-checkbox"
                       type="checkbox"
                       value="${z.id}"
                       id="zone-${z.id}"
                       ${s.selectedZoneIds.includes(z.id) ? 'checked' : ''}/>

                <label class="form-check-label"
                       for="zone-${z.id}">
                    ${z.name}
                </label>
            </div>`
        ).join('');

        $('save-settings').addEventListener('click', async () => {
            const ids = [
                ...document.querySelectorAll('.zone-checkbox:checked')
            ].map(c => c.value);

            const r = await fetch('/api/notification-settings', {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(
                    buildSettingsPayload(
                        $('notifications-enabled').checked,
                        ids
                    )
                )
            });

            if (
                r.ok &&
                $('notifications-enabled').checked &&
                typeof enablePush === 'function' &&
                Notification.permission !== 'granted'
            ) {
                await enablePush();
            }

            $('settings-status').textContent =
                r.ok
                    ? 'Settings saved.'
                    : 'Could not save settings.';
        });
    });
}

if (typeof module !== 'undefined') {
    module.exports = {
        buildSettingsPayload
    };
}