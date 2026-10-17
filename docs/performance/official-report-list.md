# Official report list performance and regression checks (T-008.7)

Measured on 17 October 2026 (Asia/Dhaka). All **50 measured trials** passed the strict **under 2,000 ms** budget. The slowest combined HTML-plus-API trial was **62.900 ms**. Full trial timings, SQL parameters, query plans, and environment data are recorded in [official-report-list.json](official-report-list.json).

## Scope and environment

The benchmark creates a dedicated temporary **file-backed SQLite database**, applies the application migrations, and seeds exactly **10,000 synthetic reports** using `DevelopmentReportSeeder`. It never opens the normal application database. Reports span all seven categories, three areas, six statuses, and four urgency levels. Each scenario uses page size 20, two warm-up loads, and five measured loads. The performance collection runs without competing xUnit tests.

A load consists of two sequential authenticated localhost HTTP requests: `/official/reports?query`, with its HTML body fully read, followed by `/api/reports?query`, with the JSON fully parsed. Timings include JWT cookie authentication, routing, SQL, Razor rendering, and response transfer. Setup, seeding, host startup, expected-count queries, assertions, and `EXPLAIN QUERY PLAN` run outside the timed interval. The SQL capture interceptor remains enabled during measurement. Browser JavaScript execution, asset downloads, DOM rendering, and paint are **not measured**; this is a server response benchmark, not a browser or production latency guarantee.

Environment recorded for this run:

- Windows build 26200, x64; CPU identifier `Intel64 Family 6 Model 142 Stepping 12, GenuineIntel`; four logical processors available to .NET.
- .NET SDK 10.0.402, runtime .NET 10.0.13, EF Core 10.0.12, SQLite 3.53.3; Release build.
- GC-reported available memory: 4,124,585,984 bytes. This is the runtime's reported memory budget, not a measurement of physical RAM.
- Kestrel on an ephemeral loopback port; SQLite connection pooling disabled. The warmed file-system cache is part of this test environment.

## Timings

Every median and maximum below is for **HTML plus API/JSON**, in milliseconds. Each row has five measured trials, in addition to its two recorded warm-ups.

| Scenario | Matching reports | Median ms | Maximum ms |
|---|---:|---:|---:|
| Newest first | 10,000 | 20.185 | 24.934 |
| Oldest first | 10,000 | 20.316 | 25.286 |
| Status ascending | 10,000 | 17.316 | 21.110 |
| Status descending | 10,000 | 28.953 | 41.295 |
| Urgency ascending | 10,000 | 50.133 | 57.258 |
| Urgency descending | 10,000 | 28.080 | 34.089 |
| Area 1 and RoadDamage | 476 | 23.310 | 30.622 |
| Area 1 and RoadDamage/WaterLogging | 953 | 24.190 | 29.273 |
| Area 2 only | 3,332 | 43.052 | 62.900 |
| Last page, page 500 | 10,000 | 31.149 | 45.923 |

## SQL and index evidence

Each load executes exactly three read queries: area choices for the HTML screen, the filtered report count, and the bounded report projection. The API returns at most 20 rows; the captured projection uses parameterized `LIMIT`/`OFFSET` and excludes report descriptions and account password fields. Counts are checked against independently aggregated fixture groups. Database report/history/notification counts remain unchanged after the measurements.

`EXPLAIN QUERY PLAN` runs against the **captured EF commands and their real parameter values**, rather than a substitute hand-written query:

- Date ascending/descending and page 500 use `IX_Reports_CreatedAt_Id`.
- Status ascending uses `IX_Reports_Status_CreatedAt_Id`.
- Urgency descending uses `IX_Reports_Urgency_CreatedAt_Id`.
- Area plus a single category uses `IX_Reports_AreaId_Category_CreatedAt_Id`.
- Area-only and area plus multiple categories use `IX_Reports_AreaId_CreatedAt_Id`.
- Status descending and urgency ascending scan the report table and use a temporary B-tree for ordering in this run. Their mixed sort directions do not match the existing indexes' full ordering. They still pass the measured 10,000-report budget; this result does not claim every sort avoids a scan or temporary sort.

No additional index or production query change was required to meet the measured target. The JSON evidence includes both count and page query plans for every scenario.

## Reproduce

From the API repository directory, run the ordinary checks and then the benchmark separately so other tests do not compete with the timed requests:

```powershell
dotnet test .\Nagorik.Tests\Nagorik.Tests.csproj --configuration Release --filter 'Category!=Performance'
$env:NAGORIK_PERF_OUTPUT = Join-Path (Get-Location) 'docs\performance\official-report-list.json'
dotnet test .\Nagorik.Tests\Nagorik.Tests.csproj --configuration Release --filter 'Category=Performance' --logger 'console;verbosity=normal'
Remove-Item Env:NAGORIK_PERF_OUTPUT
```

Without `NAGORIK_PERF_OUTPUT`, the benchmark writes its JSON evidence to a unique temporary file and reports that path through the test output. It always checks all measured loads against the two-second budget; the warm-ups are recorded but excluded from that assertion. The temporary database is cleaned up when the test finishes. Timings vary with host load, hardware, and runtime.

## Regression coverage

The T-008.7 additions validate query defaults, accepted category names, numeric/unknown/excess category rejection, sort/direction validation, positive area/page bounds, and offset overflow protection. HTTP tests traverse **every page** for all six sort orders with date ties spanning page boundaries, then repeat the final page and request the page beyond the end. Filtered traversals combine multiple categories with an area and span multiple pages. Assertions cover exact ordering, membership, no duplicates or omissions, stable total counts, fixed page size, no-store responses, SQL pagination, and read-only behavior at the highest valid page.

Existing integration tests continue to cover anonymous/resident/official/CityOfficial authorization, unauthorized queries making no reads, category OR plus area AND filtering, optional fields, empty results, safe official detail access, and preserved list navigation. This task passed **300 ordinary .NET tests plus one performance test (301 total)**. Browser UI checks are not part of this benchmark run.
