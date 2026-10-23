# Remaining non-ML acceptance criteria

Scope: NAG-031, NAG-032, NAG-033 and NAG-035. NAG-034 (all six ML tasks)
is excluded. None of these four stories requires ML: NAG-030's rule engine
supplies risk levels and history. Historical scores are human-supplied rule inputs,
not a trained model. No ML package, dataset or model service is required.

Each criterion below is a completion requirement, not a claim that it has passed.
Verification results are recorded in the corresponding story notes.

## NAG-031 — crew map

| Task / owner | Acceptance criteria |
|---|---|
| T-031.1 Swapnil | Migration preserves existing crews with unknown location; coordinates must be a valid complete pair with a UTC last-seen timestamp; invalid pairs/ranges are rejected. |
| T-031.2 Labiba | A crew can explicitly choose to share GPS with a successful update of their own assigned task. Permission denial, timeout or skipping GPS still permits a status update. Failed/unauthorized task updates do not alter location. Location is never inferred from the report's address. |
| T-031.3 Redwan | Dispatcher-only `GET /api/map/crews` returns GeoJSON with crew name, busy/free state, last-seen and stale indicator; unknown locations are omitted and counted. Resident/anonymous callers cannot retrieve crew locations. |
| T-031.4 Rajiya | Dispatcher map has an independent crew layer toggle, distinct busy/free/stale markers, safe text popups, refresh and explicit failure/empty states. |

## NAG-032 — rule-risk operations

| Task / owner | Acceptance criteria |
|---|---|
| T-032.1 Labiba | A committed transition into High or Severe creates a durable alert event; unchanged levels do not. Notifications reuse `INotificationService` and area opt-in/preferences. No legacy seeded risk is presented as a new canonical prediction. Event processing survives application restart. |
| T-032.2 Redwan | Dispatcher-only endpoint lists drainage points in currently evaluated High/Severe zones, with point status, zone risk and evaluation time. Unknown/Low/Medium and outdated evaluations are excluded. |
| T-032.3 Rajiya | Dispatcher panel shows flagged points and available/busy crews, supports quick assignment through the existing report/crew workflow, explicitly confirms busy crews and prevents duplicate active work for the same point. Unauthorized/invalid requests cannot create work. |

## NAG-033 — resident/public map

| Task / owner | Acceptance criteria |
|---|---|
| T-033.1 Redwan | Public endpoints expose zone risk/boundaries, explicitly verified report markers and blocked drainage only. Responses contain no crew location, user identity, contact information, private photo URLs or raw report description/address. Unverified reports never appear. |
| T-033.2 Rajiya | Resident map renders zones, verified reports and blockages using local Leaflet assets, with legend, toggles, safe text popups, refresh and honest empty/error/sample-data states. |
| T-033.3 Rajiya | Locate me requests GPS only after a click, identifies containing Polygon/MultiPolygon zone including holes, displays its stored risk and evaluation timestamp, and handles denied/unavailable GPS or outside-zone location without uploading coordinates. |
| T-033.4 Labiba | Dispatcher can explicitly verify/unverify a report using an authenticated, CSRF-protected action with stale-edit detection. Review records reviewer/time; public markers reflect the new state after refresh. Other roles cannot verify. Verification is distinct from workflow approval/completion. |

## NAG-035 — canonical area subscriptions

| Task / owner | Acceptance criteria |
|---|---|
| T-035.1 Swapnil | Resident-zone subscription has resident/zone foreign keys, alerts enabled flag and a unique resident-zone constraint. Migration does not drop existing legacy subscriptions. |
| T-035.2 Redwan | Resident-only endpoints list their own choices, subscribe/update enabled state and unsubscribe. Zone IDs and payloads are validated, repeated subscribe is idempotent and users cannot supply another resident's ID. Mutations require CSRF. |
| T-035.3 Rajiya | My areas page lists real canonical zones, saved selections and alerts state; supports add/remove/enable/disable and optional locate-me suggestions, handles errors and never auto-subscribes without confirmation. |
| T-035.4 Labiba | Rule-risk alert recipient lookup includes enabled canonical subscriptions, respects resident/global opt-outs and deduplicates any legacy overlap. Disabling/unsubscribing prevents subsequent events from notifying that resident. |
| T-035.5 Labiba | Durable per-event/user receipts prevent duplicate successful notifications after repeated polling/restart; per-user/zone cooldown limits repeated elevated transitions. Failed delivery is visible and retried with bounded attempts. Concurrent processors cannot independently claim the same delivery. External push cannot promise mathematically exactly-once delivery across a crash. |
| T-035.6 Rajiya | Notifications identify the zone and new risk, say rule-based assessment, include evaluation time and link to the resident map focused on that zone. Text does not promise a confirmed flood or imply live GPS tracking. |

## Verification policy

Use isolated SQLite databases for schema/service/HTTP tests, meaningful tests for
role boundaries, stale edits and duplicate prevention, JavaScript tests for GPS and
UI failures, and headless browser smoke coverage where practical. Preserve the
working demo database and unrelated local edits. Commit and push each task to its
own `feature/T-xxx.x-*` branch, with both author and committer set to its owner.

## Implementation and verification record

All 17 included tasks are implemented. NAG-034 remains excluded; no included
story depends on ML. Local verification: 678 backend tests passed, 139 client
tests passed, and all six isolated headless browser scenarios were verified across
the serial regression run and targeted rerun. The final targeted run passed 3/3,
including the complete new workflow and both utility viewport cases. Earlier
browser failures were corrected in the harness (serial execution, stale-element
handling, conventional routes and waiting for actual worker activation); one
mobile CDP transport timeout was resolved on the isolated rerun.

| Story | Main evidence |
|---|---|
| NAG-031 | CrewLocationSchemaTests, CrewLocationWorkflowTests, CrewTasksApiTests, crew GeoJSON HTTP tests, crew-location/dispatcher-crews client tests and real crew GPS/status/busy/free browser flow. |
| NAG-032 | RiskComputationTests, ZoneRiskAlertTests, flagged-point HTTP tests, DrainageDispatchTests, protected panel tests and real assignment/duplicate-work browser flow. |
| NAG-033 | Anonymous/privacy and verification HTTP tests, resident-map and zone-locator client tests, real public publication/GPS browser flow. |
| NAG-035 | ResidentZoneSubscriptionSchemaTests, private/idempotent/CSRF subscription HTTP tests, ZoneRiskRecipientTests, RiskAlertDeliveryTests, message tests, client subscription/global/push tests and real area/global-preference browser flow. |
| Cross-story | NonMlMigrationTests proves upgrade/rollback preserves existing crews and legacy opt-ins. EF reports no pending model changes. The real service worker activates on the isolated browser server. |

Operational conditions remain explicit: real GPS depends on browser/OS permission
and availability; real push needs valid VAPID keys, device permission and a saved
endpoint. Browser tests mock successful notification transport rather than sending
real external push messages. Rule coefficients are experimental, and cached or
missing inputs are never presented as verified flood forecasts. See
`NON_ML_RUNBOOK.md` for setup, routes and demonstration steps.
