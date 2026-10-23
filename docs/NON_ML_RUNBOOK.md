# Non-ML stories: running and reviewing

Implemented scope: NAG-031, NAG-032, NAG-033 and NAG-035 (17 tasks).
NAG-034's six ML tasks are excluded. No other included story needs ML; all use the
existing NAG-030 rule-risk engine. There is no model-training/model-serving service.
Acceptance criteria are in `NON_ML_ACCEPTANCE_CRITERIA.md`.

## Start locally

From `F:\Nagorik\Nagorik.Api`, stop the running application before migration:

```powershell
dotnet restore
dotnet ef database update
dotnet run --launch-profile http
```

The HTTP profile runs at `http://localhost:5000` in Development. The existing
development seeder provisions the accounts below with password `Test@12345`.
Production accounts/roles must be provisioned separately. Schema tests use
isolated SQLite; implementation did not migrate or rewrite the working demo DB.

| Role | Development email | Starting URL |
|---|---|---|
| Resident | resident1@test.com | `/resident/areas` or `/map` |
| Dispatcher | dispatcher1@test.com | `/dispatcher/map` |
| Crew | crew1@test.com | `/crew/tasks` |
| Admin | admin1@test.com | `/admin/risk-thresholds` |
| City official | official1@test.com | `/official/reports` |

Use `/login.html?returnUrl=%2Fresident%2Fareas` (or another local destination).
Existing browser sessions need a refresh/re-login after the app restarts.

## Routes in presentation order

1. **Public map:** `/map`. Show zone colors, stored evaluation timestamps,
   verified report markers and blocked drainage. It is anonymous. Select Locate
   me to match GPS locally; clear removes that local marker. A point outside the
   supplied boundaries does not mean Low risk. Synthetic zone boundaries are
   labelled. No crew/identity/private report fields appear on this map.
2. **Admin rule inputs:** `/admin/risk-thresholds`. Enter reviewed vulnerability
   inputs and increasing thresholds, save, and evaluate zones. Missing/stale
   weather or vulnerability gives Unknown. Open-Meteo collection needs internet.
   Actual rainfall can legitimately produce Low risk and an empty flagged panel;
   do not claim High/Severe results when the inputs do not support them.
3. **Dispatcher map:** `/dispatcher/map`. Inspect zone factors, drainage status,
   report clusters/photos and crew positions. F/B/? means free/busy/stale. Unknown
   crew positions are counted but not invented. Positions older than 15 minutes
   are stale. These are task-update samples, not continuous tracking.
4. **Crew GPS:** first assign an Approved City Corporation report using the
   existing dispatcher report workflow. As that crew open `/crew/tasks/{id}`,
   optionally capture GPS, then Start/Complete task. Skipping/denying GPS still
   permits the task update. Refresh the dispatcher map to see a successful sample.
5. **Flagged operations:** `/dispatcher/flagged-drainage`. Recently evaluated
   High/Severe zones produce flagged points. Choose a crew and explicitly confirm
   busy crews when appropriate. A single linked work report prevents duplicate
   active inspections. The assigned report uses the existing crew workflow.
6. **Public verification:** `/dispatcher/report-verification`. Inspect one of the
   latest 100 reports, verify a located marker, then refresh `/map` anonymously.
   Unverify and refresh to hide it. Review time/user/revision are recorded.
   This decision is separate from work approval/completion and publishes only
   coordinates, generated reference, category/status/time.
7. **My areas:** `/resident/areas`. Pick a zone, add it, switch its alerts off/on,
   and remove it. GPS suggests a picker choice without saving automatically.
   Global state is visible; adding an area never enables it silently.
8. **Area-risk alert preferences:** `/Notifications/Settings`. Enable or disable
   global area-risk alerts without changing memberships. Enable push on this
   device explicitly. The app needs valid `Vapid:Subject`, `Vapid:PublicKey` and
   `Vapid:PrivateKey` configuration, notification permission and a saved browser
   push subscription. Permission alone does not prove a subscription exists.
9. **Delivery diagnostics:** `/admin/risk-alert-deliveries`. Inspect Sent, Failed
   and Suppressed receipts/attempts. No registered endpoint or invalid push config
   leads to Failed and bounded retries, not a fabricated successful delivery.

GPS depends on the browser/OS provider and permission; localhost is usable, but a
desktop or embedded browser may be unable to obtain a fix. The app handles that
failure and keeps manual map/area selection and task updates available.

## API checks

Anonymous public reads (PowerShell's `curl.exe` avoids its old alias):

```powershell
curl.exe http://localhost:5000/api/public/map/risk-zones
curl.exe http://localhost:5000/api/public/map/reports
curl.exe http://localhost:5000/api/public/map/blockages
```

Dispatcher authenticated reads: `/api/map/crews`, `/api/map/flagged-drainage`,
`/api/map/weather`, `/api/map/risk-zones`, `/api/map/drainage-points`,
`/api/map/reports`. Other roles cannot retrieve crew locations or dispatcher data.

Resident authenticated reads: `/api/resident/areas` and
`/api/resident/areas/security-token`. Use the same auth/antiforgery cookies and the
returned `RequestVerificationToken` header for PUT/DELETE mutations:

- `PUT /api/resident/areas/{zoneId}`: `{"alertsEnabled":true}` or false.
- `DELETE /api/resident/areas/{zoneId}`: removes membership and matching legacy opt-in.
- `PUT /api/resident/areas/global-preference`: `{"enabled":true}` or false,
  preserving all memberships.

## Alert behavior and limits

Only committed changes into High/Severe generate events. The minute worker checks
current recipients and opt-outs, creates durable unique event/resident receipts,
and uses a database lease to serialize processors. Unchanged levels generate no
new event. The default cooldown is 60 minutes per resident/zone; unconfirmed sends
retry at five-minute intervals up to three attempts. Old/inapplicable evaluations
are suppressed. See `NAG-035-my-areas.md` for configuration and crash-window limits.
An accepted push endpoint does not prove the person saw the notification.

## Automated checks

```powershell
dotnet test Nagorik.Tests --verbosity quiet
node --test ClientTests/*.test.cjs
dotnet test Nagorik.UiTests --filter "FullyQualifiedName~DispatcherMapE2eTests|FullyQualifiedName~UtilityAnomalyE2eTests"
dotnet ef migrations has-pending-model-changes --no-build
```

The browser fixtures are headless, use temporary databases and local test accounts,
and run serially to avoid competing Chrome instances. GPS and elevated risk are
explicit fixture data in tests, not readings inserted into the working demo DB.
The legacy `ServiceWorkerTests` harness separately requires an app at
`NAGORIK_URL`/localhost:5000; the integrated scenario instead activates the real
service worker on its own isolated server.

Every task is a separate owner-attributed commit on a `feature/T-031.*`,
`feature/T-032.*`, `feature/T-033.*` or `feature/T-035.*` branch. The final branch
contains the dependency chain. No automatic merge to main or ML implementation
is part of this scope.

## Recorded local verification

678 backend tests and 139 client tests passed. All six isolated browser scenarios
passed across the serial regression and targeted rerun (final targeted run 3/3).
The full new browser workflow verifies actual GPS capture, role changes, database
persistence, assignment deduplication, publication filtering, service-worker
activation and membership-preserving global preference updates. No live external
push was sent; deterministic sender fakes test delivery success/failure and retry
semantics. EF reports no pending model changes. Existing unrelated local edits
and viva/demo artifacts were preserved outside the task commits.
