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
