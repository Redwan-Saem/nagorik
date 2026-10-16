(() => {
    const task = document.querySelector('[data-task-id]');
    if (!task) return;
    const feedback = document.getElementById('task-feedback');
    const successKey = `crew-task-updated-${task.dataset.taskId}`;
    try {
        if (sessionStorage.getItem(successKey)) {
            sessionStorage.removeItem(successKey);
            feedback.textContent = 'Task status updated successfully.';
            feedback.focus();
        }
    } catch { /* Storage may be unavailable; updating tasks still works. */ }

    const button = document.getElementById('update-task-status');
    if (!button) return;
    const error = document.getElementById('task-error');
    const login = document.getElementById('task-login');
    const showError = message => {
        feedback.textContent = '';
        error.textContent = message;
        error.hidden = false;
        error.focus();
    };

    button.addEventListener('click', async () => {
        if (button.disabled) return;
        button.disabled = true;
        task.setAttribute('aria-busy', 'true');
        error.hidden = true;
        feedback.textContent = 'Updating task status…';
        try {
            const response = await fetch(`/api/crew/tasks/${encodeURIComponent(task.dataset.taskId)}/status`, {
                method: 'PATCH',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify({ status: button.dataset.nextStatus })
            });
            if (response.ok) {
                try { sessionStorage.setItem(successKey, '1'); } catch { }
                window.location.reload();
                return;
            }
            if (response.status === 401) {
                login.hidden = false;
                login.href = `/login.html?returnUrl=${encodeURIComponent(window.location.pathname)}`;
                showError('Your session has expired. Log in again to continue.');
            } else if (response.status === 403) {
                showError('You do not have permission to update crew tasks.');
            } else if (response.status === 404) {
                showError('This task is no longer available to your crew. Return to My Tasks.');
            } else if (response.status === 409) {
                showError('The task status has changed or this update is no longer allowed. Refresh the task to see its current status.');
            } else {
                showError('The update could not be confirmed. Refresh the task to check its status before trying again.');
            }
        } catch {
            showError('The update could not be confirmed. Check your connection and refresh the task before trying again.');
        } finally {
            task.removeAttribute('aria-busy');
        }
    });
})();
