# NAG-028 — drainage and reports on the dispatcher map

| Task | Planned owner | Implementation |
| --- | --- | --- |
| T-028.1 | Saem / Redwan | Dispatcher-only drainage GeoJSON |
| T-028.2 | Saem / Redwan | Report GeoJSON with safe photo URL projection |
| T-028.3 | Tisha / Rajiya | Drainage status icons, popups and layer toggle |
| T-028.4 | Tisha / Rajiya | Clustered report markers, photo/status/location popups |
| T-028.5 | Labiba | CSRF-protected blocked/clear editing with expected-state check |

Each task is committed on a separate dependent `codex/T-028.*` branch. The final
branch includes the whole story on top of NAG-027; a push does not merge main.
Owner-attributed Git metadata is not proof of independent human cross-testing.

## Run and navigate

Stop the running application first. Back up the local SQLite file before migrations.
No new migration is needed beyond NAG-027's Zones and DrainagePoints migrations.

```powershell
dotnet ef database update --project .\Nagorik.Api.csproj
$env:DevelopmentData__SeedMapSamples = 'true'
# Optional: 84 synthetic reports so the report layer has data without manual submissions.
$env:DevelopmentData__SeedSampleReports = 'true'
dotnet run --project .\Nagorik.Api.csproj --launch-profile http
```

Open `http://localhost:5000/login.html?returnUrl=%2Fdispatcher%2Fmap` and log in as
`dispatcher1@test.com` / `Test@12345` unless that account's password was changed.
The page is `http://localhost:5000/dispatcher/map`.

1. Expect three sample zones and six drainage points on a fresh Development DB.
2. Toggle zones, drainage and reports independently.
3. Drainage icons use ✓ Clear, ! Blocked, ? Unknown.
4. Click a drainage marker; choose **Mark blocked** or **Mark clear**.
5. Confirm feedback/icon changes, then refresh to verify persistence.
6. A second editor with an old displayed status receives conflict feedback and must refresh.
7. Reports with usable coordinates appear as blue R markers or counted clusters.
8. Click a small cluster to spread its reports; large clusters zoom to their bounds.
9. Click an individual report for its reference, location, status, primary photo/fallback
   and an **Open dispatcher report** link.
10. Deny access as Resident/Crew/Official or anonymously; these operational layers
    are not the public/resident map planned in NAG-033.

## API contracts

- `GET /api/map/drainage-points`: GeoJSON FeatureCollection. Features use Point
  geometry `[longitude, latitude]`, stable point id, status and zone id/code/name.
- `GET /api/map/reports`: Point FeatureCollection. Properties contain reference,
  title, location, category, status, primary photo URL and UTC creation time.
- Both GET routes require Dispatcher, use no-store and cap responses at 5,000
  features in deterministic order. `omittedCount` explicitly reports omitted rows.
- Missing/invalid report coordinates, including the legacy 0/0 sentinel for
  address-only reports, are omitted, not plotted at a fabricated position. They
  remain accessible through report lists/details. The omitted count also includes
  records beyond the cap. This is not a complete viewport/pagination API.
- Photo URLs are limited to generated local report-photo routes. User identifiers,
  subscription details and private filesystem paths are not projected.
- `PATCH /api/map/drainage-points/{id}/status` expects JSON:

```json
{ "status": "Blocked", "expectedStatus": "Clear" }
```

The cookie-authenticated PATCH additionally requires a matching anti-forgery
cookie and `RequestVerificationToken` header from the hidden token on the map page.
Only Clear/Blocked targets are allowed; expectedStatus may also be Unknown.
Successful writes return `{ "id": 1, "status": "Blocked" }`. Missing ID is 404,
invalid input/token is 400, stale displayed state is 409, unauthenticated is 401
for API requests, and other authenticated roles are 403. The status comparison and
update are one SQL statement; it is an expected-state check, not a monotonic
version token or a new audit/history subsystem.

## Tests

```powershell
dotnet test .\Nagorik.Tests\Nagorik.Tests.csproj
node --test ClientTests/*.test.cjs
dotnet test .\Nagorik.UiTests\Nagorik.UiTests.csproj --filter FullyQualifiedName~DispatcherMapE2eTests
```

Query/API tests verify GeoJSON order, safe photo projection, missing coordinates,
empty states, no-store, role denial, anti-forgery, status validation, stale-state
rejection and saved status. Client tests cover symbols, toggles, safe popup text,
cluster behavior, photo fallback, failed loads, expected-state writes and duplicate
clicks. Selenium uses real login and isolated migrated SQLite, tests desktop/mobile
layers, actual authorized image delivery, cluster expansion, blocked/clear controls
and persistence after reload. Automated fixtures do not mutate the user's normal DB.

## Limits and dependencies

The map uses synthetic Development boundaries/risk samples from NAG-027; changing
drainage status does not yet recompute risk or send alerts. Those belong to later
stories. No crew layer/public verification is introduced here. General report
approval and the old general-report submission-token issue remain separate.
Reports lacking a real captured coordinate cannot be geocoded from their address
by this story. Legacy files may be missing; the popup shows a photo fallback.

Leaflet.markercluster 1.5.3 and its MIT license are served locally. Background OSM
tiles still require internet. Each layer handles failures separately; one failed
endpoint does not discard successful layers. A failed edit is not blindly replayed;
the UI asks the dispatcher to refresh before retrying.
