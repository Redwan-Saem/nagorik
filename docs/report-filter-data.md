# Report filter schema and development fixtures (T-008.1)

`Report` keeps its existing Category, Status, Location, and CreatedAt fields. `AreaId` is a nullable FK to `ReportAreas`; null means the area has not been classified. Existing address text and coordinates are preserved without guessing an area. The initial area lookup contains Dhanmondi, Mirpur, and Gulshan, matching place names already used in this project. It does not define ward or zone boundaries.

`Urgency` is an integer-backed enum ordered Low (0), Normal (1), High (2), Critical (3). Existing reports and new reports without an explicit urgency default to Normal. A database check rejects unknown values, and explicit Low values remain Low. Category filter values and labels come from `ReportLookups.Categories`, derived from the existing `ReportCategory` enum.

Indexes cover newest-first ordering with an Id tie-breaker, category, area, combined area/category, status ordering, and urgency ordering. SQLite query-plan tests check the combined area/category index against a 10,000-report dataset.

The repeatable HTTP benchmark, environment, timings, and captured EF query plans are documented in [T-008.7 performance results](performance/official-report-list.md). Its isolated temporary database does not require enabling the development flags below.

## Optional development datasets

Both flags default to false in `appsettings.Development.json`. They are ignored outside the Development environment, even if explicitly enabled. Migrate the database before starting the app.

To enable the 84-report sample set in PowerShell:

```powershell
$env:DevelopmentData__SeedSampleReports = "true"
dotnet run
```

For a separate 10,000-report performance set:

```powershell
$env:DevelopmentData__SeedPerformanceReports = "true"
dotnet run
```

Enabling both produces 10,084 fixture reports. Remove the relevant environment settings to stop future seeding. Disabling a flag does not delete existing fixtures. Restarting with the same flags fills missing fixture titles and does not duplicate unchanged fixture records or overwrite existing reports.

Fixtures belong to isolated `.invalid` development accounts with random passwords; report titles start with `DEV-SAMPLE-` or `DEV-PERF-`. They span categories, areas, statuses, urgencies, locations, and timestamps. Task statuses reference a separate fixture crew. The seeder writes synthetic current report states directly, without sending notifications or simulating a complete workflow audit trail. Do not use these accounts for real reports or responder configuration.
