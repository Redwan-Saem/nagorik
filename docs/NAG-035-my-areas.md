# NAG-035 — My areas and canonical risk notifications

See `NON_ML_ACCEPTANCE_CRITERIA.md`. This story depends on canonical rule-based
risk and the resident map, not ML.

T-035.1 (Swapnil): canonical resident-zone subscriptions have enabled state,
creation/update timestamps, unique resident-zone membership and restrictive
foreign keys. The migration adds a table; legacy AreaSubscriptions remain intact.

T-035.2 (Redwan): Resident-only `GET /api/resident/areas`,
`GET /api/resident/areas/security-token`, `PUT /api/resident/areas/{zoneId}` with
`{"alertsEnabled":true|false}`, and `DELETE /api/resident/areas/{zoneId}`. Mutations
require the antiforgery cookie and RequestVerificationToken header. Identity is
taken only from the authenticated resident; clients cannot select another user.
Subscribe is an atomic idempotent upsert. Unsubscribe removes the matching legacy
opt-in too, preventing unexpected fallback alerts. Global notification preferences
are never silently enabled by adding an area.

T-035.3 (Rajiya): `/resident/areas` has a real zone picker, saved memberships,
per-area alert toggles and remove buttons. It surfaces global opt-out state and
links to existing global/device push settings. Optional GPS only suggests a zone;
the user must confirm saving. Coordinates are matched locally and never uploaded.
The page uses safe text nodes, CSRF-protected writes and explicit failure feedback.
