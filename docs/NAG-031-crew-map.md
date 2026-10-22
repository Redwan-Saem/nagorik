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
