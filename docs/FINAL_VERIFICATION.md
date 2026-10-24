# Final non-ML verification — 25 October 2026

## Verified results

| Check | Result |
|---|---|
| Complete backend suite after privacy correction | 791 passed, 0 failed, 0 skipped |
| Client JavaScript suite | 163 passed, 0 failed |
| Full browser recheck | 21 passed; 1 desktop automatic-poll timeout, resolved on focused rerun below |
| Focused desktop/mobile schedule refresh + offline recovery rerun | 2 passed, 0 failed |
| Photo/submission focused regression | 135 passed, including five new HTTP cases |
| EF pending-model check | No changes since the latest migration |
| Live local smoke | 15 public/authenticated page/API requests returned 200; anonymous admin API returned 401 |
| Owner identity audit before final verification commit | All 51 commits beyond fetched main have matching assigned author and committer |

The initial browser run had 21/23 passes: an external-server-dependent worker
test and a mobile navigation assertion failed. The harness now owns its worker
server, removes the empty placeholder test and handles the actual mobile menu.
The subsequent complete 22-case run passed 21, including desktop/mobile dispatcher
maps and the worker. Its remaining failure was the desktop automatic-publication
poll timing out. That test now explicitly foregrounds the page (production polling
pauses while hidden), asserts visibility and includes diagnostic state on timeout.
The isolated desktop/mobile rerun passed both cases, preserving the real 30-second
poll and 45-second wait, including offline reload/reconnection. No deterministic
production defect was reproduced; timing under a busy machine remains a browser
test sensitivity. This is a full-suite result plus a focused rerun, not a claim of
a separate clean 22-case run after the final harness adjustment.

Local reports: `outputs/test-results/final-backend-recheck.trx`,
`final-ui-recheck.trx` and `resident-poll-recheck.trx`. The earlier failed reports
are retained locally rather than overwritten or represented as passing.

## Local runtime and data

The application was started in Development at `http://localhost:5000/`, with
weather collection, automatic risk evaluation and risk-alert delivery disabled.
The smoke checks covered the homepage, public map, transport/occupancy, water/waste,
air quality, public catalog/zone APIs and resident, dispatcher, crew, city official,
utility official and admin entry pages. No report, schedule or external emergency
notification was created by those smoke requests. Seed logins were exercised.

All pending database migrations had been applied after a consistent SQLite backup
in `outputs/local-runtime/`; no pending EF model change remains. Backups and TRX
files are local ignored artifacts, not public repository contents.

## Reproduction

```powershell
dotnet test Nagorik.Tests --logger 'trx;LogFileName=backend.trx' --results-directory outputs/test-results
node --test (Get-ChildItem ClientTests/*.test.cjs).FullName
dotnet test Nagorik.UiTests --logger 'trx;LogFileName=ui.trx' --results-directory outputs/test-results
dotnet ef migrations has-pending-model-changes
```

The backend/browser HTTP fixtures use isolated temporary databases and local
servers. Chrome and a compatible driver are needed for Selenium. No real GPS
provider accuracy, external push receipt or calibrated flood accuracy is claimed.
See `NAG-038-external-acceptance.md`. Operational geography/catalogs and official
publications still need responsible staff input; demo samples are labelled.

## Git delivery

Individual tasks retain separate `feature/` branches and owner-attributed commits.
The final cumulative PR targets `main`. It is not automatically merged. Use a
regular merge preserving commits if per-contributor commit history is required;
squashing would replace those individual commits on the default branch.
GitHub contribution statistics may refresh later and depend on account-email
association. A pushed, unmerged branch alone does not update main's history.
