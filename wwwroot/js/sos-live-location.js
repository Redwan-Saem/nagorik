(() => {
    class SosLiveLocation {
        constructor({ geolocation = navigator.geolocation, fetcher = fetch, now = Date.now,
            onStatus = () => {}, onError = () => {}, onAlert = () => {}, onLocation = () => {},
            intervalMs = 10000, distanceMeters = 25 } = {}) {
            Object.assign(this, { geolocation, fetcher, now, onStatus, onError, onAlert, onLocation, intervalMs, distanceMeters });
            this.running = false;
            this.generation = 0;
        }
        async start(id) {
            this.stop();
            const generation = this.generation;
            this.abort = new AbortController();
            try {
                const response = await this.fetcher(`/api/sos/${encodeURIComponent(id)}`, {
                    credentials: 'same-origin', headers: { Accept: 'application/json' }, signal: this.abort.signal
                });
                if (generation !== this.generation) return false;
                if (!response.ok) throw new Error('Cannot access this alert. Refresh the page or log in again.');
                const alert = await response.json();
                if (generation !== this.generation) return false;
                this.onAlert(alert);
                if (!['Active', 'Responding'].includes(alert.status)) throw new Error('This alert is no longer active.');
                if (!this.geolocation) throw new Error('GPS is unavailable. Enter a manual address instead.');
                this.id = id;
                this.address = alert.addressText;
                this.running = true;
                this.lastAttempt = -Infinity;
                this.lastSent = null;
                this.busy = false;
                this.onStatus('Live location sharing enabled. Waiting for GPS…');
                const watch = this.geolocation.watchPosition(p => this.position(p, generation), error => {
                    if (generation !== this.generation) return;
                    if (error.code === 1) this.stop();
                    this.onError(error.code === 1 ? 'GPS permission was denied. Enter a manual address instead.' :
                        'GPS is temporarily unavailable. You can enter a manual address.');
                }, { enableHighAccuracy: true, maximumAge: 10000, timeout: 15000 });
                if (!this.running) { this.geolocation.clearWatch(watch); return false; }
                this.watch = watch;
                this.poll = setInterval(() => this.checkActive(generation), 15000);
                return true;
            } catch (error) {
                if (generation === this.generation) { this.stop(); this.onError(error.message); }
                return false;
            }
        }
        async position(position, generation) {
            if (!this.running || generation !== this.generation || this.busy) return;
            const point = { latitude: position.coords.latitude, longitude: position.coords.longitude };
            if (!Number.isFinite(point.latitude) || !Number.isFinite(point.longitude) ||
                Math.abs(point.latitude) > 90 || Math.abs(point.longitude) > 180) return;
            const time = this.now();
            if (time - this.lastAttempt < this.intervalMs) return;
            if (this.lastSent && this.distance(this.lastSent, point) < this.distanceMeters && time - this.lastSaved < 30000) return;
            this.lastAttempt = time;
            this.busy = true;
            try {
                const response = await this.fetcher(`/api/sos/${encodeURIComponent(this.id)}/location`, {
                    method: 'PATCH', credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                    body: JSON.stringify({ ...point, addressText: this.address }), signal: this.abort.signal
                });
                if (generation !== this.generation) return;
                if ([401, 403, 404, 409].includes(response.status)) {
                    this.stop();
                    this.onError('Live sharing stopped: the alert or your session changed. Refresh the page.');
                    return;
                }
                if (!response.ok) throw new Error('Location was not saved. Check your connection or enter a manual address.');
                this.lastSent = point;
                this.lastSaved = time;
                this.onLocation(point);
                this.onStatus(`GPS updated: ${point.latitude.toFixed(5)}, ${point.longitude.toFixed(5)}.`);
            } catch (error) {
                if (generation === this.generation) this.onError(error.message);
            } finally {
                if (generation === this.generation) this.busy = false;
            }
        }
        async checkActive(generation) {
            if (!this.running || generation !== this.generation || this.polling) return;
            this.polling = true;
            try {
                const response = await this.fetcher(`/api/sos/${encodeURIComponent(this.id)}`, {
                    credentials: 'same-origin', headers: { Accept: 'application/json' }, signal: this.abort.signal
                });
                if (generation !== this.generation) return;
                if ([401, 403, 404].includes(response.status)) {
                    this.stop(); this.onError('Live sharing stopped. Refresh the page or log in again.'); return;
                }
                if (!response.ok) throw new Error('Cannot confirm the alert status. Check your connection.');
                const alert = await response.json();
                if (generation !== this.generation) return;
                this.address = alert.addressText;
                this.onAlert(alert);
                if (!['Active', 'Responding'].includes(alert.status)) {
                    this.stop(); this.onStatus(`Alert ${alert.status}. Live location sharing stopped.`);
                }
            } catch (error) {
                if (generation === this.generation) this.onError(error.message);
            } finally { if (generation === this.generation) this.polling = false; }
        }
        stop() {
            this.generation++;
            this.running = false;
            this.polling = false;
            if (this.watch !== undefined) this.geolocation.clearWatch(this.watch);
            this.watch = undefined;
            if (this.poll !== undefined) clearInterval(this.poll);
            this.poll = undefined;
            this.abort?.abort();
        }
        distance(a, b) {
            const radians = Math.PI / 180;
            const lat = (b.latitude - a.latitude) * radians;
            const lon = (b.longitude - a.longitude) * radians;
            const h = Math.sin(lat / 2) ** 2 + Math.cos(a.latitude * radians) * Math.cos(b.latitude * radians) * Math.sin(lon / 2) ** 2;
            return 6371000 * 2 * Math.atan2(Math.sqrt(h), Math.sqrt(Math.max(0, 1 - h)));
        }
    }
    globalThis.SosLiveLocation = SosLiveLocation;
})();
