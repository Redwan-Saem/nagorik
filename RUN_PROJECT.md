# Run and demonstrate Nagorik

The application is a single .NET 10 MVC/API service with SQLite and plain browser
JavaScript. There is no separate frontend build, npm install, ML process or database
server. Run commands from `F:\Nagorik\Nagorik.Api` in PowerShell.

## Setup and start

```powershell
Set-Location F:\Nagorik\Nagorik.Api
dotnet restore
```

If `dotnet ef` is unavailable, install the matching CLI once:
`dotnet tool install --global dotnet-ef --version 10.0.12`.

Configure `Jwt:Key` in the existing secret store, or for a fresh local terminal
generate an environment-only development signing key without printing it:

```powershell
$jwtBytes = [byte[]]::new(48)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($jwtBytes)
$env:Jwt__Key = [Convert]::ToBase64String($jwtBytes)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

Keep the same key for a session; changing it invalidates previously issued login
cookies. Do not replace an existing configured production key with a temporary one.
Stop the old application before schema updates; preserve the database.

```powershell
dotnet ef database update
$env:RiskAlerts__Enabled = 'false'
dotnet run --launch-profile http
```

Open **[http://localhost:5000/](http://localhost:5000/)**. Startup should print
`Now listening on: http://localhost:5000`. The http profile sets Development.
SQLite is `nagorik.db` in this directory. Development startup adds missing test
accounts and clearly labelled map/transport samples; it does not reset passwords
or publish fictional water, waste or loadshedding plans. Production does not seed
the sample catalogs. New installations need curated data/publication.

For a quiet local demo, optionally set `Weather__Enabled=false` and
`RiskJob__Enabled=false` before starting. Those flags stop automatic weather/risk
cycles; missing current data remains Unknown. To demonstrate collection/evaluation,
remove those overrides and restart with provider network access. The final local
smoke run uses these quiet flags plus disabled risk-alert delivery.

Risk alert delivery is intentionally disabled in the command above. Enable it only
for a deliberate configured-device test. External push also needs `Vapid:Subject`,
`Vapid:PublicKey`, `Vapid:PrivateKey`, browser permission and a saved device
subscription. Keep the private key in secrets/configuration. Production cookies are
Secure and require HTTPS; local HTTP is supported only in Development.

## Development accounts

Initial password for these Development seed accounts: **`Test@12345`**.
Existing account passwords are never reset by seeding.

| Role | Email | Start page |
|---|---|---|
| Resident | resident1@test.com / resident2@test.com | `/` |
| Dispatcher | dispatcher1@test.com | `/dispatcher/map` |
| Crew | crew1@test.com / crew2@test.com | `/crew/tasks` |
| City official | official1@test.com | `/official/reports` |
| Utility official | utility1@test.com | `/official/loadshedding` |
| Admin | admin1@test.com | `/admin/data` |

Use the brand to return home. Phone navigation is under **Menu**. Use **Sign out**
before changing roles, or use separate browser profiles to keep resident/official
sessions side by side. Creating a resident account is available at `/register.html`.

## All main page URLs in one place

| Page | URL |
|---|---|
| Homepage / workspace | http://localhost:5000/ |
| Dashboard alias | http://localhost:5000/dashboard |
| Sign in | http://localhost:5000/login.html |
| Register | http://localhost:5000/register.html |
| Public risk map | http://localhost:5000/map |
| Transport planner | http://localhost:5000/transport |
| Bus occupancy | http://localhost:5000/transport/occupancy |
| Water schedule | http://localhost:5000/services/water |
| Waste schedule | http://localhost:5000/services/waste |
| Air quality | http://localhost:5000/air-quality |
| Resident loadshedding | http://localhost:5000/resident/loadshedding |
| My areas | http://localhost:5000/resident/areas |
| Report a civic issue | http://localhost:5000/Reports/Create |
| My reports | http://localhost:5000/Reports/Mine |
| Utility anomaly report | http://localhost:5000/reports/utility-anomalies/create |
| Noise concerns / tracking | http://localhost:5000/resident/concerns/noise |
| Construction concerns / tracking | http://localhost:5000/resident/concerns/construction |
| Notification settings | http://localhost:5000/Notifications/Settings |
| SOS | http://localhost:5000/sos |
| My active SOS | http://localhost:5000/sos/active |
| Dispatcher map | http://localhost:5000/dispatcher/map |
| Flagged drainage | http://localhost:5000/dispatcher/flagged-drainage |
| Public-marker verification | http://localhost:5000/dispatcher/report-verification |
| Crew tasks | http://localhost:5000/crew/tasks |
| City reports | http://localhost:5000/official/reports |
| Loadshedding management | http://localhost:5000/official/loadshedding |
| Water management | http://localhost:5000/official/services/water |
| Waste management | http://localhost:5000/official/services/waste |
| Noise review | http://localhost:5000/official/concerns/noise |
| Construction review | http://localhost:5000/official/concerns/construction |
| Geographic / transport data | http://localhost:5000/admin/data |
| Risk thresholds / evaluation | http://localhost:5000/admin/risk-thresholds |
| Alert delivery diagnostics | http://localhost:5000/admin/risk-alert-deliveries |

Detail links must use IDs returned by the app, not invented examples. Open a report
from its list, a crew task from My tasks, an SOS responder link from its authorized
alert, a published-plan detail from management, and concern details from tracking.
Old links such as `/sos/respond/123` are not proof that alert 123 exists or is yours.

## A practical 15-minute presentation

1. **0–1 min — Homepage.** “Nagorik brings reporting, published services and area
   information into one role-based workspace. The same server enforces access.”
   Show guest cards, sign in as a resident and show the changed workspace.
2. **1–3 min — Public map and transport.** Show source/sample labels and Unknown
   risk honestly. Plan **Dhanmondi 27 → Motijheel**, compare fastest/cheapest and
   open steps/detail. Explain the Development graph/fares are synthetic catalog
   fixtures, not live operator routes, and lines are schematic.
3. **3–5 min — Reporting.** Submit a real demo civic report with a small PNG/JPG
   and a Dhaka location, then open My reports. Use the existing report/dispatch
   pages to show assignment and crew progress with separate role sessions.
4. **5–7 min — Service planning.** In a utility session generate/review/publish a
   loadshedding week. In the resident session show it appearing on refresh or the
   30-second poll. Show Unknown before publication and the cache notice offline.
5. **7–9 min — Water/waste.** Utility officials manage water; city officials manage
   waste. Create an explicitly labelled faculty-demo bulletin/window, save Draft,
   publish with confirmation, then show the seven-day public page and source.
6. **9–11 min — Noise/construction.** Submit a recent concern with manual coordinates
   `23.75, 90.37`. Switch to city review, start an investigation, record a disposition,
   then return to resident history. Reports/decibels/references remain unverified
   observations until reviewed; the app does not infer legal compliance.
7. **11–12 min — Air quality/occupancy.** Show provider source/model/download time.
   If unavailable, show truthful Unknown/Stale. Report bus crowding, show its public
   distribution and explain 15-minute expiry and five-minute update cooldown.
8. **12–14 min — SOS and admin.** Use a deliberate review/confirm action only on a
   demo account; manual address fallback works if GPS is unavailable. Explain
   responder access and tracking stops on page exit. Show geographic source/audit
   management and curated transport export/import without inventing survey accuracy.
9. **14–15 min — Evidence.** Show test results and remaining external acceptance:
   physical-device GPS, a configured push delivery and real calibration/survey data.
   These are not established by deterministic browser fixtures. NAG-034 ML is excluded.

Prepare demo publications first; the initial water/waste/loadshedding pages are
allowed to be Unknown. Do not present synthetic operating data or staged inspection
notes as real city facts. Do not promise offline updates or external delivery.

## Independent API checks

Public checks require no login:

```powershell
$base = 'http://localhost:5000'
Invoke-RestMethod "$base/api/public/map/risk-zones"
Invoke-RestMethod "$base/api/transport/places"
Invoke-RestMethod "$base/api/transport/routes?originLat=23.7562&originLon=90.3758&destinationLat=23.7330&destinationLon=90.4172&sort=cheapest"
Invoke-RestMethod "$base/api/transport/occupancy/segments"
Invoke-RestMethod "$base/api/services/zones"
Invoke-RestMethod "$base/api/services/water?zoneId=1"
Invoke-RestMethod "$base/api/services/waste?zoneId=1"
Invoke-RestMethod "$base/api/air-quality?zoneId=1"
```

Use actual IDs from the zone/segment APIs. Unknown/empty is a valid response when
no current data is published/reported. Route details use an option ID plus the same
coordinate/sort query. Protected API examples and exact contracts are in the story
documents linked below; retain one cookie session and its CSRF token for writes.

```powershell
$body = @{ email='resident1@test.com'; password='Test@12345' } | ConvertTo-Json
Invoke-RestMethod "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body $body -SessionVariable residentSession | Out-Null
Invoke-RestMethod "$base/api/resident/loadshedding?zoneId=1" -WebSession $residentSession
$csrf = (Invoke-RestMethod "$base/api/transport/occupancy/security-token" -WebSession $residentSession).requestToken
# Pick a real enabled Bus segment ID from /api/transport/occupancy/segments.
$observation = @{ linkId=1; level='Standing' } | ConvertTo-Json
Invoke-RestMethod "$base/api/transport/occupancy" -Method Post -ContentType 'application/json' -Headers @{ RequestVerificationToken=$csrf } -Body $observation -WebSession $residentSession
```

## Pre-demo smoke checklist

- Homepage renders; resident login and sign-out work in the browser you will share.
- Public map loads canonical zones; sample/provenance/Unknown labels are visible.
- Dhanmondi 27 → Motijheel returns at least two demo catalog options, with detail.
- Correct officials can open water/waste/loadshedding management; resident cannot.
- A deliberately prepared publication appears on the resident/public weekly page.
- My reports and crew tasks show actual records; detail IDs come from lists.
- A demo noise/construction concern has a visible recorded review history.
- Air-quality failure shows Unknown/Stale; occupancy without fresh reports is Unknown.
- SOS uses manual fallback if the OS/browser cannot obtain GPS; no real emergency
  or external notification is triggered unintentionally.
- Keep the real-device evidence limitations and test-results summary ready.

## Tests and feature notes

Stop the demo server with **Ctrl+C** before running commands that rebuild it;
Windows otherwise locks the running executable. Restart it after testing.

```powershell
dotnet test Nagorik.Tests
node --test (Get-ChildItem ClientTests/*.test.cjs).FullName
dotnet test Nagorik.UiTests
dotnet ef migrations has-pending-model-changes
```

Node is needed only for the optional client tests. Selenium tests need Chrome and
a compatible ChromeDriver (Selenium Manager can locate/cache it); they launch
headless browsers and isolated local servers. They do not need your demo server,
real push delivery or provider accuracy. Save TRX results with `--logger trx`.

- [Completion record](docs/PROJECT_COMPLETION.md)
- [Official/resident loadshedding](docs/NAG-016-loadshedding.md), [resident cache/GPS](docs/NAG-015-resident-schedules.md)
- [Transport](docs/NAG-018-transport.md), [occupancy](docs/NAG-019-bus-occupancy.md)
- [Water/waste](docs/NAG-010-011-service-schedules.md), [air quality](docs/NAG-012-air-quality.md)
- [Noise/construction](docs/NAG-013-014-civic-concerns.md)
- [Managed data](docs/NAG-037-managed-data.md), [homepage](docs/NAG-040-homepage.md)
- [Integration corrections](docs/NAG-039-integration.md)
