# NAG-018 — transport routes and fares

Scope: directed stored transport graph, configurable BDT fares, multiple loop-free
options, fastest/cheapest/fewest-transfer ordering, safe detail, local landmark
suggestions, optional GPS and map pins. Coordinates must be within the explicitly
bounded Dhaka service rectangle (23.60–23.95 N, 90.20–90.58 E). Equal points fail.
No network or missing data may produce invented routes or fares.

| Task | Owner | Acceptance |
|---|---|---|
| T-018.1 | Redwan | Relational stops, directed links and configurable fare rules; reversible migration; Development-only labelled samples. |
| T-018.2 | Swapnil | Bounded graph search with multiple options where available, area/input validation, transfers/time/distance and no-route state. |
| T-018.3 | Redwan | Per-mode BDT fares, contiguous boarding legs, estimated ranges, explicit missing-rule handling. |
| T-018.4 | Swapnil | Deterministic sorting and detail endpoint exposing leg directions and geometry. |
| T-018.5 | Labiba | Local catalog suggestions, click-only GPS, map pins and schematic route drawing. |
| T-018.6 | Labiba | Accessible origin/destination form, no-search validation, retained inputs. |
| T-018.7 | Rajiya | Responsive options, sort control and selected detail with per-leg fare and map. |
| T-018.8 | Labiba | Bounded timeout/retry, honest empty/error/sample states and meaningful API/client/browser checks. |

Sample routes and fare coefficients are demonstration fixtures, not official
operator service or tariff claims. Stored link distances/times are estimates.
Map lines connect catalog stops schematically, not turn-by-turn road navigation.
Only enabled catalog landmarks have typed suggestions; arbitrary street geocoding
is not supplied by the local provider. GPS and map pins cover other coordinates
where a supported stop is within walking range. Production is empty until curated
data is imported. No dependency on ML.

## Demonstration and API

Stop the app, run `dotnet ef database update`, then
`dotnet run --launch-profile http`. In Development, the sample catalog is inserted
only if no stops exist. Open `http://localhost:5000/transport` (public).
Choose **Dhanmondi 27** to **Motijheel**. Compare fastest and cheapest, then choose
**Show steps on this page** or **Open route detail page**. The latter preserves
the coordinates, names, sort order and route ID in the URL.

Optional GPS needs browser/OS location permission. Map clicks set the endpoint
selected in the Map click sets control. Stops must be within 1.2 km of each point.
Walking connectors use straight-line estimates and must be checked for safety.
An unknown typed address, missing/same points or locations outside the explicit
service rectangle do not trigger a route request.

Public API examples (no login token required):

```powershell
curl.exe "http://localhost:5000/api/transport/places?q=Farmgate"
curl.exe "http://localhost:5000/api/transport/routes?originLat=23.7562&originLon=90.3758&destinationLat=23.7330&destinationLon=90.4172&sort=cheapest"
```

Use an option's `id` at `/api/transport/routes/{id}` with the same coordinate/sort
query to inspect detail. Invalid inputs return 400; an unavailable route ID returns
404. No route returns 200 with an empty options array and an explicit message.
The graph is capped at 500 enabled stops, 5,000 links, 10 links per path,
20,000 expansions and four seconds of computation; a limited result is labelled
`truncated`. Up to 48 candidate paths are compared and 12 options returned, so
sorting compares available candidates and does not claim a citywide optimum.
Missing fare rules display Fare unavailable. Routes and prices are not cached as
live information when offline. Retry preserves input. No query coordinates are
stored in a dedicated history, but ordinary host access logging may record URLs.

## Verification

Seven backend tests passed: Development-only/idempotent samples, database
constraints, directed connectivity/no-route, invalid coordinates, timing, minimum
fare/ranges, single-board charging, missing-fare totals, API sorting/detail and
safe public fields. Eight client tests passed, covering GPS/manual fallback,
request validation, retry/input retention, stale-response cancellation, text-only
rendering and safe detail URLs. No pending EF model changes were reported.
Two Chrome browser cases passed at 390px and 1280px: real form validation,
landmark search, cheapest sorting, detail URL/reload, deterministic GPS,
offline request failure, retained inputs, recovery and horizontal overflow.
The GPS fixture verifies browser integration, not physical-device accuracy.
