(() => {
    class SosSubmissionError extends Error {
        constructor(message, status = 0, transient = false) {
            super(message); this.status = status; this.transient = transient;
        }
    }
    class SosSubmission {
        constructor({ fetcher = (...args) => globalThis.fetch(...args), storage, uuid = () => crypto.randomUUID(), online = () => navigator.onLine !== false,
            onRetry = () => {}, attempts = 3, timeoutMs = 20000, sleep } = {}) {
            Object.assign(this, { fetcher, uuid, online, onRetry, attempts, timeoutMs });
            try { this.storage = storage ?? sessionStorage; this.requestId = this.storage.getItem('sos-pending-request'); } catch { }
            if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(this.requestId || '')) this.requestId = null;
            this.sleep = sleep || ((ms, signal) => new Promise((resolve, reject) => {
                const abort = () => { clearTimeout(timer); reject(new DOMException('Request stopped.', 'AbortError')); };
                const timer = setTimeout(() => { signal.removeEventListener('abort', abort); resolve(); }, ms);
                signal.addEventListener('abort', abort, { once: true });
            }));
        }
        submit(payload) {
            if (this.result) return Promise.resolve(this.result);
            if (this.pending) return this.pending;
            this.controller = new AbortController();
            this.pending = this.run(payload, this.controller.signal).finally(() => { this.pending = null; });
            return this.pending;
        }
        cancel() { this.controller?.abort(); }
        async run(payload, signal) {
            if (!this.requestId) {
                this.requestId = this.uuid();
                try { this.storage?.setItem('sos-pending-request', this.requestId); } catch { }
            }
            for (let attempt = 0; attempt < this.attempts; attempt++) {
                signal.throwIfAborted();
                if (!this.online()) throw new SosSubmissionError('You are offline. Reconnect, then choose Check and retry.');
                try {
                    // Recover the exact submission even if it was cancelled while the response was lost.
                    const existing = await this.request(`/api/sos/requests/${encodeURIComponent(this.requestId)}`, {}, signal);
                    let alert;
                    if (existing.status === 404) {
                        alert = (await this.request('/api/sos', {
                            method: 'POST', headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ ...payload, clientRequestId: this.requestId })
                        }, signal)).data;
                    } else alert = existing.data;
                    if (!Number.isInteger(alert?.id) || alert.id <= 0 ||
                        !['Active', 'Responding', 'Cancelled', 'Completed'].includes(alert.status))
                        throw new SosSubmissionError('The response could not be confirmed. Check and retry to recover your alert.', 0, true);
                    signal.throwIfAborted();
                    this.result = alert;
                    try { this.storage?.removeItem('sos-pending-request'); } catch { }
                    return alert;
                } catch (error) {
                    if (signal.aborted || error.name === 'AbortError') throw error;
                    const transient = !(error instanceof SosSubmissionError) || error.transient;
                    if (!transient || attempt === this.attempts - 1) throw error;
                    const delay = 1000 * (2 ** attempt);
                    this.onRetry(attempt + 2, delay);
                    await this.sleep(delay, signal);
                }
            }
        }
        async request(url, options, signal) {
            signal.throwIfAborted();
            const controller = new AbortController();
            const abort = () => controller.abort();
            signal.addEventListener('abort', abort, { once: true });
            let timer;
            try {
                return await Promise.race([
                    (async () => {
                        const response = await this.fetcher(url, {
                            ...options, credentials: 'same-origin', cache: 'no-store',
                            headers: { Accept: 'application/json', ...options.headers }, signal: controller.signal
                        });
                        if (response.status === 404 && !options.method) return { status: 404 };
                        if (!response.ok) {
                            const status = response.status;
                            const message = status === 401 ? 'Your session expired. Log in again before retrying.' :
                                status === 409 ? 'An SOS alert already exists. Check your active alert.' :
                                status === 400 ? 'The SOS location was rejected. Go back and review it.' :
                                'The server could not confirm this request. Check your active alert or retry.';
                            throw new SosSubmissionError(message, status, status >= 500 || status === 408 || status === 429);
                        }
                        return { status: response.status, data: await response.json() };
                    })(),
                    new Promise((_, reject) => {
                        timer = setTimeout(() => {
                            reject(new SosSubmissionError('The request timed out. Check and retry to recover your alert.', 0, true));
                            controller.abort();
                        }, this.timeoutMs);
                    })
                ]);
            } finally { clearTimeout(timer); signal.removeEventListener('abort', abort); }
        }
    }
    globalThis.SosSubmission = SosSubmission;
})();
