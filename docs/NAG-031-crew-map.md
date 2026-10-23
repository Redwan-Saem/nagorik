# NAG-031 — crew locations

Acceptance criteria: `NON_ML_ACCEPTANCE_CRITERIA.md`.

T-031.1 (Swapnil): nullable crew latitude/longitude/last-seen fields and migration;
database check requires all three together and validates ranges. Existing crews
remain unknown. No task/report coordinate is copied into a crew's GPS location.

T-031.2 (Labiba): task detail has explicit capture/skip buttons; denied GPS does
not block status changes. Optional coordinates are validated and saved with the
owned report transition in the same SaveChanges transaction. Conflict rolls both
back. A status update without GPS preserves the earlier location timestamp.

T-031.3 (Redwan): `GET /api/map/crews` is Dispatcher-only GeoJSON. Busy means an
Assigned/InProgress report exists. Unknown locations are omitted and counted;
locations older than 15 minutes are explicitly stale, not current tracking. No
email, user ID or phone is exposed. Responses are no-store and capped at 5000.

T-031.4 (Rajiya): dispatcher map includes an independent crew layer with F/B/?
markers, recent/stale labels, last-seen UTC, safe name popups, refresh, empty/error
states and no continuous tracking. Unknown GPS cannot create an invented marker.

Verified per-task: 5 schema tests, 16 workflow tests, 48 crew API tests, 34 map/API
tests, and dedicated client tests. Apply `dotnet ef database update` with the app
stopped before running; the working demo database is not changed by these tests.
