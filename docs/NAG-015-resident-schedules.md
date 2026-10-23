# NAG-015 — resident loadshedding schedules

Depends on NAG-016's canonical publication pointers. Draft demand, official audit
and unpublished plans must never be returned by the resident endpoint. Date
boundaries and all outage labels use Asia/Dhaka, irrespective of device timezone.

| Task | Owner | Acceptance |
|---|---|---|
| T-015.1 | Redwan | Indexed query returns exactly today plus six days, current published slots only, next/ongoing outage and actual publication time. No publication differs from a published day with no outages; partial replacements use the correct version per day. |
| T-015.2 | Swapnil | Resident-only GET validates zone ID, returns 404 for missing zones, no-store data and safe fields. Drafts, capacities, demands, official identity and audit are excluded. |
| T-015.3 | Labiba | Click-only GPS matches canonical polygons locally, requests no location at page load, supports manual change, denied/unavailable/outside-area fallback and overlapping-zone choices. Coordinates are never uploaded. |
| T-015.4 | Rajiya | Navigation opens the real seven-day screen with today highlighted, clear empty/partial states, last updated and next/ongoing outage. Safe text rendering and narrow-screen layout. |
| T-015.5 | Labiba | Cache the last successful published response by zone, show offline/stale timestamps honestly, poll for publication changes, retain entered selection on failure, and ignore obsolete responses when switching zones. No cache fallback on auth/authorization errors. |
| T-015.6 | Redwan | Meaningful API, date-boundary, publication-isolation, partial-replacement, cache/GPS/error and real browser coverage; verify normal-load query under two seconds. |

GPS depends on OS/browser permission and provider availability. A point outside
the configured boundaries must not be assigned to an invented nearest zone.
Offline data is only the last downloaded plan; it cannot prove the current grid
status or receive publication changes until a connection returns.

## Run and demonstrate

Apply NAG-016's migration with the app stopped (`dotnet ef database update`), then
run `dotnet run --launch-profile http`. NAG-015 itself needs no new migration.

1. As `utility1@test.com` / initial password `Test@12345`, open
   `http://localhost:5000/official/loadshedding`. Generate and publish a week for
   selected zones. Review and explicitly acknowledge the planning warnings.
2. Sign in as `resident1@test.com` / initial password `Test@12345` and open
   `http://localhost:5000/resident/loadshedding`. Existing passwords are not reset.
3. Choose a zone. The page shows exactly today plus six days in Asia/Dhaka, with
   today's card highlighted, actual publication/download timestamps and the next
   or ongoing planned outage. Each unpublished day remains Unknown, even when a
   different published day explicitly has no outage.
4. Optionally choose **Use my location**. A single containing canonical polygon
   selects that zone locally. Missing, ambiguous or outside boundaries retain the
   manual picker. No permission is requested at page load and coordinates are
   never sent to the app. **Clear location request** invalidates late callbacks.
5. Publish a replacement as an official in another browser session. The visible
   resident page checks every 30 seconds; Refresh also checks immediately.
6. After the service worker has activated and the plan has loaded online, set the
   browser's Network panel to Offline, refresh the schedule, then reload the page.
   A generic offline shell displays the last downloaded public plan with an
   explicit cache notice and coverage/download dates. It cannot confirm current
   authentication, actual power status or newer publications. Reconnect and
   refresh to restore the live server view.

The offline shell and scripts contain no user-specific content. API responses,
authenticated HTML, auth cookies and tokens are never cached by the worker.
Only the public schedule DTO and zone catalog are stored in local storage. Cache
entries expire after seven days or when their entire date coverage has elapsed.
Storage-disabled browsers retain live functionality but cannot promise offline
reopening. A real HTTP 401/403 clears the visible plan and asks the resident to sign
in; a cache never overrides those responses. GPS cannot match offline unless
boundaries can be loaded, so manual cached-zone selection remains available.

## API

- `GET /api/resident/loadshedding/zones`: available canonical zone IDs/code/name.
- `GET /api/resident/loadshedding?zoneId=1`: seven days, publication states,
  safe outage times, next/ongoing outage and real timestamps. Use an ID from zones.
- Both endpoints require Resident and send no-store. Missing/invalid ID is 400;
  nonexistent zone is 404; unauthenticated requests are 401 and other roles 403.
- The result never contains draft slots, official identity, demand/capacity or
  audit records. Refresh re-queries canonical per-zone/day publication pointers.

## Verification

Tests cover Dhaka midnight, next/ongoing outage, empty published weeks, hidden
draft/superseded pointers, partial republishing and a warm indexed read under two
seconds with 2,520 historical slots. Client tests cover GPS privacy/cancellation,
offline/auth/cache failures, response ordering, timeouts and preservation of push
handling. Desktop/mobile Chrome coverage exercises the real 30-second poll and
offline reload using deterministic GPS fixture coordinates, not an actual GPS fix.

Executed: six resident query tests, eight focused API tests, 155 client tests in
the working checkout, and two resident browser cases passed. The full backend
run passed 751 of 752 tests; the existing official-report-list two-second
performance assertion failed, then passed unchanged when rerun on its own. This
suggests sensitivity to execution load, rather than proving a root cause. No test
threshold was relaxed. EF reports no pending model changes.

The existing cross-feature crew/dispatcher/public-map/resident-area browser case
also passed on a diagnostic rerun (31 seconds). Its first quiet run failed without
an actionable diagnostic; that initial failure is retained here. The rerun used
the same code and thresholds and covered worker activation and GPS fixture flows.
