# NAG-029 — weather data collection

| Task | Planned owner | Delivered |
| --- | --- | --- |
| T-029.1 | Ekanto / Swapnil | RainfallReading entity, constraints, indexes and migration |
| T-029.2 | Labiba | Open-Meteo client and validated past/forecast accumulations |
| T-029.3 | Labiba | Immediate/hourly BackgroundService and atomic per-zone upserts |
| T-029.4 | Saem / Redwan | Bounded retry/timeout handling and explicit stale-cache status |

Each task has its own owner-attributed commit and dependent branch. The final
`codex/T-029.4-weather-resilience` branch contains the whole story on top of
NAG-027/028. Pushes do not merge main. Git identity is task attribution, not proof
of independent human cross-testing; automated verification was run by the agent.

## Start and inspect

Stop the application and back up `nagorik.db` before applying migrations:

```powershell
dotnet ef database update --project .\Nagorik.Api.csproj
$env:Weather__Enabled = 'true'
$env:DevelopmentData__SeedMapSamples = 'true'
dotnet run --project .\Nagorik.Api.csproj --launch-profile http
```

Normal startup begins one collection cycle immediately, then waits 60 minutes
after completion. The scheduler does not overlap its own cycles. It creates and
disposes a DI scope per cycle. Individual zone failures do not stop the remaining
zones; host cancellation interrupts requests/backoff/scheduled waits.

Log in as dispatcher at:

```text
http://localhost:5000/login.html?returnUrl=%2Fapi%2Fmap%2Fweather
http://localhost:5000/api/map/weather
```

Development credentials: `dispatcher1@test.com` / `Test@12345`, unless changed.
The endpoint is Dispatcher-only and no-store. It reports per-zone past/forecast
data, UTC window/fetch times, source, age, IsStale, and Available/Partial/Stale/
Unavailable state. Cold startup may show Unavailable until the first request
finishes. No data is represented by null snapshots, not zero rain.

The existing `/dispatcher/map` continues to display its existing risk values.
NAG-029 is collection/backend work; weather factors/risk recomputation belong to
NAG-030. No new weather page or manual unauthenticated collection endpoint is added.

## Provider and units

Official API contract: [Open-Meteo forecast documentation](https://open-meteo.com/en/docs).
Source: [Open-Meteo](https://open-meteo.com/). Review provider terms/attribution for
deployment; this implementation does not include a commercial API key.

The client requests hourly `rain,showers`, millimetres, UTC and Unix timestamps.
Open-Meteo labels these by the END of the preceding hour. Given UTC anchor H:

- Past 1h uses H; past 3h uses H-2 through H; past 6h uses H-5 through H.
- Forecast 1h uses H+1; forecast 3h H+1 through H+3; forecast 6h H+1 through H+6.
- Rain + showers are summed. Columns store accumulated mm, not mean mm/hour or a
  flood-risk score. Past values are provider model data, not ground-station labels.
- Missing/null/negative/non-finite values, unexpected units, duplicate/gapped time
  series, malformed JSON and incomplete windows are failures, never implicit zeros.
- Weather is sampled at a zone boundary bounding-box centre. This is an approximate
  representative point, not a surveyed station; concave/multi-part zones may need
  an explicitly verified sampling point later. Malformed coordinate structures are
  skipped. This is not a full geographic topology-validation subsystem.

## Persistence and fallback

RainfallReadings references Zone with restricted deletion. The unique key is
zone + UTC anchor + forecast flag + source. A repeated poll for the same hour
updates the existing pair, not duplicate rows. Different hours are retained.
Past and forecast rows commit together; failed persistence rolls both back.

Provider failure leaves prior values and their actual fetch/window times intact.
The read endpoint selects the latest pair in SQL and checks age against both
timestamps. It does not refresh the age of old data merely because a collection
failed. With no prior pair it returns Unavailable; old data is Stale. Data is not
copied into a fresh row or substituted as zero to hide an outage.

Retries apply to network errors, timeouts, HTTP 408/429/5xx, up to MaxRetries extra
attempts. Other HTTP errors and malformed/invalid payloads fail immediately.
Backoff is exponential and bounded to 30 seconds; short Retry-After values are
considered. A Retry-After above 30 seconds defers without an immediate retry, to
avoid blocking the collector indefinitely. There is no persistent cross-cycle
provider cooldown: the next attempt occurs on a later scheduled cycle. Each
request attempt has a separate timeout and a 1 MiB response buffer limit.

## Configuration

`appsettings.json` Weather values can be overridden by environment variables:

| Setting | Default | Accepted range |
| --- | --- | --- |
| Enabled | true | true/false |
| PollMinutes | 60 | 15–1440 |
| RequestTimeoutSeconds | 15 | 1–60 |
| MaxRetries | 2 | 0–3 extra attempts |
| RetryDelayMilliseconds | 1000 | 0–5000 |
| MaxAgeMinutes | 90 | 15–1440 |

Example: `$env:Weather__Enabled = 'false'` disables collection but does not delete
readings. Invalid settings fail startup validation. Migration generation/tests do
not apply the schema or collect weather into the normal local database.

## Verification

```powershell
dotnet test .\Nagorik.Tests\Nagorik.Tests.csproj --filter "FullyQualifiedName~RainfallSchemaTests|FullyQualifiedName~Weather|FullyQualifiedName~MapApiTests"
dotnet test .\Nagorik.Tests\Nagorik.Tests.csproj
node --test ClientTests/*.test.cjs
dotnet test .\Nagorik.UiTests\Nagorik.UiTests.csproj --filter "FullyQualifiedName~DispatcherMapE2eTests|FullyQualifiedName~UtilityAnomalyE2eTests"
```

Schema tests exercise migrations/rollback and amount/uniqueness constraints.
Provider tests use deterministic fake HTTP responses for exact window arithmetic,
invalid data, retries, permanent errors, long Retry-After and cancellation/timeout.
Collector tests verify immediate/disabled worker behavior, failure isolation, atomic
pair persistence, upserts and cache preservation. Query/API tests cover latest
selection, aging, absent readings and role protection. Tests do not depend on a live
provider; a separate read-only live probe checks the real API contract.

## Remaining scope

This story does not calculate risk, change Zone.LastComputedAtUtc, trigger area
alerts, evaluate an ML model, provision official boundaries, or supply labelled
ground truth. Those remain later stories. Multiple app instances can contend on
the unique key; this is not a distributed exactly-once scheduler. There is no
history-retention job yet. The unrelated `/weatherforecast` template endpoint
remains random template data and must not be confused with `/api/map/weather`.
