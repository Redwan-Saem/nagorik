# NAG 041 Backup power planner

Market/building backup-power allocation extends Nagorik's published electricity
planning. Electricity officials publish zone outages; a manager supplies generator,
fuel and shop requirements. The result is advice for a responsible operator, not
automatic switching, measured load data or electrical safety certification.

## Access and workflow

- Open `/backup-power` or its homepage/header card. Manual planning is public.
- The built-in worked example is clearly illustrative: five shops, 10 kW,
  8.00 litres, six outage hours. Generate to obtain a proven value of 84 and
  8.00 litres. Equally optimal rosters may differ from the printed example.
- Paste/upload JSON, generate, inspect the board and explanations, download input
  or exact API output, or print the feasible roster on landscape A4.
- The board shows all ten slots, including neutral grid hours. Hourly load,
  hourly fuel, cumulative fuel and per-shop dark-hour totals are recomputed.
- Resident/electricity-official accounts can import a published zone/day through
  `/api/backup-power/published-grid?zoneId=1&date=yyyy-MM-dd`.
- Unknown/unpublished days are refused, not converted to grid-on. Dates outside
  the current seven-day view or partial-hour windows are refused without silent
  approximation. Only `grid_available` is replaced; imported hours are a snapshot.
- Changed inputs invalidate/hide the old result. Import and generate again after
  publication changes. No background replan or external notification is sent.
- No market plans are stored server-side. No new market role or migration is
  required for this first version. Inputs are kept in the open page and downloaded
  only by explicit action; there is no automatic local-storage persistence.

Development seeding also provisions `dpdc1@test.com` with initial password
`Test@12345` and role `DPDCOfficial`. Existing account passwords are not reset.
This role already has the existing loadshedding management permission.

## Input contract

`POST /api/roster`, `Content-Type: application/json`, at most 128 KiB. JSON fields
are snake_case and unknown fields/wrong numeric types are rejected. Schema and
validator companion files referenced in the supplied PDF were not provided;
this is Nagorik's documented contract, not a claim of exact companion-pack parity.

| Field | Requirements |
|---|---|
| grid_available | Exactly ten booleans, slots 0–9 = 10:00–20:00 Asia/Dhaka |
| generator_capacity_kw | Integer 1–1,000,000 |
| fuel_budget_litres | Nonnegative decimal up to 1,000,000, at most two decimal places |
| fuel_table | 1–1,000 strictly increasing positive integer load_kw levels, last equals capacity; each litres_per_hour follows the same decimal bounds |
| max_consecutive_dark | Integer 0–10; defaults to 2 when omitted, explicit null is invalid |
| shops | Array of 0–12 objects |
| shops[].id | Unique, nonblank string of at most 80 characters, no surrounding whitespace |
| shops[].kw | Integer 1–1,000,000; demand may exceed generator capacity |
| shops[].type | Exactly mandatory or normal |
| shops[].value_per_slot | Exactly ten nonnegative integers up to 1,000,000 |

Limits on field sizes and numeric magnitudes bound public-endpoint resource use.
There is no rounding tolerance for fuel. Litres are converted to integer hundredths.
Fuel is charged once at the first band covering combined hourly load; zero load
costs zero fuel. Grid hours consume no generator fuel/value and reset dark runs.
Mandatory shops are on during each outage. Empty shops and all-grid days are valid.

## Solver and result checker

`BackupPowerSolver` uses the pinned Google OR-Tools CP-SAT native runtime, without
ML or network calls. Binary shop/hour decisions, capacity, stepped hourly fuel,
whole-day budget, mandatory loads and sliding dark-run constraints are optimized
together. The objective counts all powered shops' values during outages only.
At most 4,096 shop subsets supply attainable-load fuel tuples. It does not perform
a naive hour-by-hour greedy allocation or enumerate every full-day roster.

Two concurrent solves per process, each using one search worker and a six-second
search limit. The limit leaves headroom for validation, model construction and HTTP
serialization. Runtime targets must still be measured on the deployment hardware;
heavy simultaneous application load can affect wall-clock time.

Application startup starts a background native warm-up using a one-variable model.
Other Nagorik pages do not wait for this optional engine. Until it is ready (or if
native initialization fails), nontrivial planner requests return HTTP 503 with an
explicit starting/unavailable message. Trivial days and mandatory-capacity conflicts
need no native engine. Library loading is separate from the six-second per-request
search limit. Tests explicitly await readiness before measuring search behavior.

Run the worked example independently of the UI (from the repository root):

```powershell
curl.exe -X POST http://localhost:5000/api/roster -H "Content-Type: application/json" --data-binary "@wwwroot/examples/backup-power-worked.json"
```

Expected: HTTP 200, `status: ok`, `optimality_proven: true`, `value_total: 84`
and `fuel_total_litres: 8`. A different equally optimal arrangement is valid.

`BackupPowerRules.Verify` independently recomputes partition completeness,
mandatory service, dark runs, loads, fuel and values before a feasible roster is
returned. It does not trust solver totals. Dark explanations cite either a concrete
capacity violation, the extra fuel of adding that shop to this hour, or the
whole-day objective/fuel/dark-run trade-off. These are not unique-cause or legal
priority determinations. The operator supplies the meaning of each value.

| HTTP | status | Meaning |
|---|---|---|
| 200 | ok | Independently checked feasible roster; inspect optimality_proven |
| 200 | infeasible | Infeasibility proven or a mandatory capacity conflict established; no unsafe roster attached |
| 400 | invalid_input | Named field/type/format errors |
| 429 | busy | Both bounded solver slots occupied; retry later |
| 503 | not_solved | No feasible solution/proof before the time limit; NOT an infeasibility claim |

`ok` includes `message`, `roster`, `value_total`, `fuel_total_litres`, `dark_hours`,
`optimality_proven`, `solver_state` and `value_upper_bound`. Each outage row has
`slot`, `hour`, `powered` IDs, `dark` entries with `id` and `reason`, `load_kw`,
`fuel_litres` and `cumulative_fuel_litres`. Grid-on slots have no roster row.
Best effort is never mislabeled optimal; a feasible time-limited result states
that optimality has not been proven. Cancellation stops native search.

## Integration and deployment

The current `LoadsheddingRotationEngine` and publication permissions are unchanged.
The adapter uses `ResidentScheduleQueryService`, reading only current published
pointers. Import does not modify schedules, user roles or operational records.
The stateless solver performs no authenticated mutation and requires no CSRF token;
the protected adapter is read-only. Existing write endpoints are unaffected.

OR-Tools adds native runtime dependencies. Restore/build before going offline and
deploy the native assets for the tested host architecture along with the app.
The planner itself does not use weather/AQI providers; optional imports need the
local Nagorik database. This does not assert the entire application runs offline.

## Acceptance criteria

1. A valid feasible result covers every outage exactly once, keeps mandatory
   shops powered, respects capacity and exact daily fuel, and limits consecutive
   dark hours with grid supply resetting the run. An independent checker verifies it.
2. The supplied worked example proves value 84 with 8.00 L; small random scenarios
   agree with an independent exhaustive oracle. Empty shops/grid-only days work.
3. Invalid inputs identify fields. Proven infeasibility, resource contention and
   a search deadline are separate outcomes; no time-limited result claims proof.
4. The page uses the real roster API, shows ten hourly columns and dark reasons,
   imports/exports JSON, prints a landscape A4 roster and fits phone navigation.
5. Editing/importing inputs invalidates the previous result and aborts late replies.
   Hidden stale results remain hidden when printing from the browser.
6. Only residents/electricity officials import published hours. Unknown days,
   unauthorized users and partial-hour schedules are refused without approximation.
7. Existing schedules remain intact. No ML, automatic switching, plan persistence
   or new manager role is introduced. Native assets must be restored before offline use.

## Task ownership

- T-041.1 Ekanto: input contract and independent validation.
- T-041.2 Redwan: whole-day solver and native runtime dependency.
- T-041.3 Redwan: stateless API, published-hours adapter and Development account.
- T-041.4 Rajiya: page, print/export, stale-result guards and navigation.
- T-041.5 Labiba: solver/oracle/API/browser verification, examples and evidence.

Automated cases cover the worked example, independent exhaustive small-instance
oracle, fuel thresholds, empty/grid-only days, oversized loads, dark-run resets,
infeasibility, changed outage input, malformed JSON and publication access/Unknown
handling. Desktop/mobile browser journeys exercise actual HTTP solving and errors.
Execution counts and measured results are recorded in the verification section
after tests run, not inferred from the presence of these cases.

## Verification status — 2026-11-04

Tested snapshot: commit 078486b, exported into a temporary checkout.

- Full backend run: 816 passed, 1 failed, 817 total. All 26 new BackupPower tests passed. The existing OfficialReportListPerformanceTests test exceeded its strict two-second budget (maximum 9.25 seconds). This is not a full-suite pass.
- The worked example proved value 84 and fuel 8.00 L. The independent small-instance oracle, API validation/publication access checks and twelve-shop timing case passed.
- All 163 existing client tests passed with Node's built-in runner from the repository root: node --test --test-concurrency=1 "ClientTests/*.test.cjs".
- The browser-test project built with zero warnings/errors before the final background-readiness edit. Four desktop/mobile Selenium cases ran and all four failed on Chrome session creation or WebDriver navigation timeouts. No successful screenshot or A4/PDF-layout verification is claimed.
- Reports: outputs/backup-power/tests/backend-final.trx and outputs/backup-power/tests/backup-ui.trx.
- The latest local startup-isolation fix starts optional native warm-up in the background and makes tests explicitly await readiness. It is outside the tested snapshot and still requires a fresh build/run.
- Five initial owner-attributed task commits are pushed on separate branches. Automatic approval review timed out twice while committing the final local startup-isolation edits. Those edits and this verification note remain local. No PR was merged automatically.
