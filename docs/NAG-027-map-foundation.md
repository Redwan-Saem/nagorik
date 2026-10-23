# NAG-027 — zones, drainage schema and dispatcher risk map

All seven tasks are implemented as sequential dependent commits on separate
`codex/T-027.*` branches. The final T-027.7 branch contains the entire story.
The branches have been pushed; pushing is not merging them into main.

| Task | Planned owner | Delivered |
| --- | --- | --- |
| T-027.1 | Ekanto / Swapnil | Zone schema and additive migration |
| T-027.2 | Ekanto / Swapnil | DrainagePoint, zone FK, status/coordinate constraints and migration |
| T-027.3 | Ekanto / Swapnil | Development-only sample Dhaka zones and drainage points |
| T-027.4 | Saem / Redwan | Dispatcher GeoJSON API with geometry checks |
| T-027.5 | Tisha / Rajiya | Leaflet/OSM dispatcher map and responsive browser coverage |
| T-027.6 | Tisha / Rajiya | Risk legend, layer toggle, safe popups and selected-zone details |
| T-027.7 | Labiba | Shared dispatcher authorization policy, navigation and role-matrix tests |

Git owner attribution is the requested task metadata, not evidence of independent
human cross-testing. Automated tests were run by the coding agent.

## Run

Stop the running app before updating its schema. Back up `nagorik.db` first.
From the repository root:

```powershell
dotnet ef database update --project .\Nagorik.Api.csproj
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DevelopmentData__SeedMapSamples = 'true'
dotnet run --project .\Nagorik.Api.csproj --launch-profile http
```

Open:

```text
http://localhost:5000/login.html?returnUrl=%2Fdispatcher%2Fmap
http://localhost:5000/dispatcher/map
http://localhost:5000/api/map/risk-zones
```

Development account: `dispatcher1@test.com` / `Test@12345` if not changed earlier.
Existing seeded passwords are not reset. Map data and page require Dispatcher;
other authenticated roles receive 403. Normal app HTML challenges redirect
anonymous visitors to login, while API requests without HTML Accept receive 401.
The map page handles an expired API session with a login link.

## Manual checks

1. Log in as dispatcher; select **Dispatcher risk map** from navigation.
2. On a fresh Development database expect three sample zones and distinct colors.
3. Check the Unknown/Low/Medium/High/Severe legend.
4. Click a polygon: inspect name, code, risk and computation timestamp/fallback.
5. Toggle **Show risk zones** off/on; refresh while hidden to confirm it stays hidden.
6. Disable internet: locally bundled Leaflet still loads; tiles may fail with guidance.
7. Open the page/API as resident, crew and official: expect denial, not zone records.

## Automated checks

Run .NET suites sequentially to avoid shared build-cache collisions:

```powershell
dotnet test .\Nagorik.Tests\Nagorik.Tests.csproj
node --test ClientTests/*.test.cjs
dotnet test .\Nagorik.UiTests\Nagorik.UiTests.csproj --filter FullyQualifiedName~DispatcherMapE2eTests
```

Schema tests check constraints and migration/rollback preservation; seeder tests
cover idempotency and production exclusion; query/API tests check GeoJSON lon/lat,
no-store, invalid rings and the role matrix. Client tests cover rendering decisions,
401/403/server errors, refresh, toggles and untrusted popup text. Selenium logs in
through the real page and checks colors, popup text, toggles and desktop/mobile layout
against an isolated migrated SQLite database. Tests never migrate the normal local DB.

## Scope and limitations

- Sample boundaries are synthetic rectangles and are labelled accordingly. Sample
  levels have no computation time. This story does not run a weather/ML predictor.
- Samples run only in Development, can be disabled, and never overwrite existing
  zone codes or add fake drainage points to pre-existing operational zones.
- Geometry checks reject malformed coordinates and unclosed/undersized rings, but
  do not prove topology correctness, absence of self-intersection or official coverage.
- The endpoint returns a FeatureCollection with stable feature IDs and properties
  `code`, `name`, `riskLevel`, `lastComputedAtUtc`. `invalidZoneCount` reports skipped
  corrupt geometry. Ring coordinates are longitude/latitude, not legacy Leaflet arrays.
- Drainage schema/sample records are present. Rendering drainage, report and crew
  layers belongs to NAG-028/NAG-031, not NAG-027. There is no drainage edit API yet.
- Existing `/map.html`, `/risk.html`, legacy map/risk rows and zone subscriptions
  remain separate and unchanged. NAG-002 area-subscription delivery can be reused
  later; transition-only alerts and cooldown are not implemented in this story.
- Leaflet 1.9.4 and its BSD license are served locally; OSM background tiles require
  internet. A failed refresh keeps old zones visible and explicitly marks them as old.
