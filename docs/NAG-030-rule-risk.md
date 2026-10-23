# NAG-030: rule-based zone risk

## T-030.1 — zone vulnerability (Swapnil)

`feature/T-030.1-zone-vulnerability` adds nullable elevation (metres), drainage
density (km/km²), and historical waterlogging score (0..1). Existing zones remain
unknown until data is supplied. These are not surveyed values and are not invented
by the weather collector. Database constraints reject out-of-range inputs.

Apply migrations with the app stopped: `dotnet ef database update`.
This task does not automatically migrate the working demo database.

## T-030.2 — rule baseline (Labiba)

`RiskPredictionService` compares hourly equivalents of both past and forecast
rainfall: maximum of 1h, 3h/3 and 6h/6. Vulnerability is
`1 + clamp((10-elevation)/10,0,1) + clamp((5-density)/5,0,1) + history`.
Score = intensity × vulnerability. Defaults are Medium ≥5, High ≥15, Severe ≥30;
lower scores are Low. Configure the `RiskScoring` section (or environment keys
`RiskScoring__MediumThreshold`, `RiskScoring__HighThreshold`,
`RiskScoring__SevereThreshold`). Thresholds must increase strictly.
Missing vulnerability, stale/missing weather, or unaligned windows produce Unknown.
These coefficients are transparent demonstration rules, not scientifically
calibrated waterlogging probabilities. Open-Meteo rainfall is model data.

## T-030.3 — scheduled computation (Redwan)

At startup and every five minutes `RiskBackgroundService` evaluates all zones,
publishing their level, evaluation timestamp and exact input/threshold snapshots
in one transaction. Missing or stale inputs yield Unknown with a reason, not a
fresh Low value. Failures are logged and retried next cycle. Configure
`RiskJob__Enabled=false` to disable or `RiskJob__PollMinutes` (1..1440).
Weather and risk jobs are independent: new weather appears on the next risk cycle.
The old `WaterloggingRisks` and `ZoneMapData` stores are not updated; this story
uses canonical `Zones`. Legacy resident alerts are a later story.

## T-030.4 — risk history (Swapnil)

Every evaluation appends a `RiskHistories` row with zone, previous/new level, UTC
time, rule version and the complete immutable input/threshold snapshot. History
and current zone state commit in the same transaction. Zone deletion is restricted
while audit rows exist. There is no public history edit/delete endpoint. This
records rule decisions, not confirmed flood labels; ML evaluation needs independent
ground truth. Retention/archival is an operational follow-up for long deployments.

## T-030.5 — contributing factors (Rajiya)

Dispatcher map popups and the accessible selected-zone panel show the stored
evaluation time, score, rainfall intensity, vulnerability, input fields, thresholds,
source window and explanation. Missing data explains Unknown. All content uses
text nodes. The GeoJSON endpoint adds a typed `factors` object; malformed legacy
factor JSON is omitted rather than breaking the whole map. Refresh zones after a
job runs. Historical popup values are snapshots, not continuous GPS/weather feeds.

## T-030.6 — Admin controls (Rajiya)

`/admin/risk-thresholds` requires the Admin role. Development startup seeds
`admin1@test.com` / `Test@12345`, using the existing development-only account
seeder. Production admins must be provisioned by the operator; registration cannot
choose roles. No dispatcher privileges are expanded to Admin.

The page validates and saves strictly increasing thresholds to a singleton database
row, with a revision token to reject stale edits. Configuration defaults apply
until the first save; database settings override configuration thereafter. Every
risk-history row includes the thresholds actually used. All POSTs require CSRF
tokens. The page also provides validated vulnerability forms and immediate
evaluation, so there is no need to edit SQLite manually. Missing or stale weather
still yields Unknown; evaluation never invents rainfall or invokes a weather fetch.

### Try it locally

1. Stop the application, then run `dotnet ef database update` in the repository root.
2. Run `dotnet run --launch-profile http` (see `Properties/launchSettings.json` for
   the configured port; the existing demo uses `http://localhost:5000`).
3. Open `/login.html?returnUrl=%2Fadmin%2Frisk-thresholds` and use the development
   admin above. Check the defaults 5 / 15 / 30. Attempt Medium=20, High=15 to see
   validation, then restore and save valid thresholds.
4. For a **synthetic demonstration only**, enter elevation `5`, drainage density
   `2`, history `0.3` for a sample zone. Real deployment requires surveyed inputs.
5. Allow the weather cycle to collect fresh aligned readings. Check
   `/api/map/weather` in a separate Dispatcher login. Weather collection requires
   internet access; database migrations must already be applied.
6. Choose **Evaluate all zones now** as Admin. Unknown is expected if inputs or
   weather are missing/stale. Otherwise each zone gets a computed baseline level.
7. Log in as `dispatcher1@test.com` / `Test@12345`, visit `/dispatcher/map`, choose
   **Refresh zones**, and click a polygon to inspect the saved factors/timestamps.

Six tasks remain on separate `feature/T-030.*` branches in dependency order. The
final branch contains the complete story; nothing is automatically merged to main.
The working local database is not automatically migrated by this implementation.

### Verification

- `dotnet test Nagorik.Tests --no-restore --verbosity quiet`: 648 passed.
- `node --test ClientTests/*.test.cjs`: 125 passed.
- Five isolated browser scenarios passed across the regression run and corrected
  Admin rerun (two dispatcher viewport cases, two utility cases, one Admin flow).
  The existing placeholder test also passes. The legacy `ServiceWorkerTests`
  failed in the full UI run; it targets a separately running `NAGORIK_URL` or
  `http://localhost:5000`, unlike these isolated fixtures. Its failure does not
  establish a defect in the new Admin workflow. `wwwroot/sw.js` still contains
  `showNotification`; the legacy test was not changed.
- `dotnet ef migrations has-pending-model-changes --no-build`: no pending changes.
- Historical drainage/rainfall rollback tests now project only zone columns that
  existed at their target migration, preserving the actual rollback assertions.
