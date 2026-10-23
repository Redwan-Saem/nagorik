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

T-035.4 (Labiba): risk-change recipients combine enabled canonical memberships and
legacy opt-ins without duplicate users. A canonical disabled choice overrides the
legacy opt-in; non-Residents and globally opted-out users are excluded. The old
seeded-risk background worker is disabled by default to avoid parallel alerts;
`Alerts__UseLegacyRiskWorker=true` is an explicit legacy compatibility switch.
The canonical worker still uses the existing `INotificationService`/push delivery.

T-035.5 (Labiba): each event/resident gets one durable receipt. A database lease
serializes processors; delivery claims and attempt counters persist across restart.
Successful sends are not reselected; repeated elevated transitions within a
resident/zone cooldown are suppressed. Recipient eligibility is checked again
before delivery, so pending retries honor opt-outs. Old/inapplicable events are
not sent. A failed or unconfirmed push retries with a bounded attempt count.

Defaults under `RiskAlerts`: Enabled=true, CooldownMinutes=60, MaxAttempts=3,
RetryMinutes=5, LeaseMinutes=2, SendTimeoutSeconds=30, MaxEventAgeMinutes=60.
An evaluation older than 15 minutes is not eligible. Admins can inspect the latest
100 receipts at `/admin/risk-alert-deliveries`; endpoint acceptance is not proof
that a person read the alert. A crash after an external push succeeds but before
the receipt is saved can still cause a retry: external delivery cannot guarantee
mathematical exactly-once behavior. The lease exceeds the bounded send timeout.

T-035.6 (Rajiya): notification copy names the zone, changed rule-risk level and UTC
evaluation time and links to `/map?zone=ID`. It describes an experimental
assessment instead of promising a flood. The global alert screen is connected to
the real canonical settings payload through a dedicated CSRF-protected global-only
update; saved memberships cannot be overwritten by that toggle. Device push setup
has one handler and resaves existing subscriptions even when permission was
already granted. Failures are shown rather than throwing an unhandled browser
error. Delivery requires valid existing VAPID configuration and device permission.

The final integrated browser scenario covers crew GPS/status, busy/free map,
flagged-point assignment/duplicate prevention, report publication, anonymous map,
actual service-worker activation, resident GPS, My areas and global preference
changes. See `NON_ML_RUNBOOK.md` for local setup, routes and honest demo limits.
