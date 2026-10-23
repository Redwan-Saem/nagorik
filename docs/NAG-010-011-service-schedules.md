# NAG-010 / NAG-011 — managed water and waste schedules

Shared scheduling infrastructure supports canonical zones, an effective period,
weekday mask (Sunday=1, Monday=2, through Saturday=64), same-day minute windows,
source and resident notes. Drafts never appear publicly. Publishing and retirement
require confirmation and the current revision; overlapping published windows fail.
Overnight windows must be split at midnight. All resident day labels use Asia/Dhaka.

Water management: provisioned UtilityOfficial/WASAOfficial or Admin. Waste:
provisioned Official/CityOfficial or Admin. Public readers get a seven-day plan and
explicit Unknown when no schedule is published. A published weekly schedule can
explicitly have no planned service on a non-selected weekday. These are planned
windows, not proof that water was delivered or waste was collected. No fake
operating schedule is seeded.

| Task | Owner | Acceptance |
|---|---|---|
| T-010.1 | Swapnil | Shared model/migration, date/time validation, draft/privacy, revision-controlled publication and weekly queries. |
| T-010.2 | Redwan | Public water API and authorized, CSRF-protected management. |
| T-010.3 | Rajiya | Water weekly view and draft/edit/publish/retire management forms. |
| T-010.4 | Labiba | Query, authorization, publish/edit conflicts and browser acceptance. |
| T-011.1 | Redwan | Waste service API with distinct official-role checks and shared validation. |
| T-011.2 | Rajiya | Waste weekly and management pages/navigation. |
| T-011.3 | Labiba | Waste-specific authorization, privacy and end-to-end acceptance. |

## Water demonstration and verification

Stop the application, run `dotnet ef database update`, then
`dotnet run --launch-profile http`. Sign in as `utility1@test.com` (initial
`Test@12345`; existing passwords remain unchanged). Open
`/official/services/water`. Select a canonical zone, a date period, weekdays,
08:00–10:00 and a source such as **Faculty demo bulletin — synthetic plan**.
Use truthful notes and save. The row is Draft; `/services/water?zoneId=<id>`
still shows Unknown. Edit the draft if necessary, then confirm publication.
The public page shows seven days, source and publication timestamps. Confirm
retirement to remove an obsolete window. Do not describe a demo plan as real WASA
service. Source text is an official's supplied reference, not independently verified.

Public API: `GET /api/services/zones`, `GET /api/services/water?zoneId=<id>`.
Official JSON clients use `GET /api/official/services/security-token` with the same
cookie jar, then the returned token in `RequestVerificationToken`. Create with
`POST /api/official/services/water/drafts` and JSON such as
`{"zoneId":1,"fromDate":"2026-10-26","toDate":"2026-11-01","daysMask":127,"startMinute":480,"endMinute":600,"source":"Demo bulletin","notes":"Synthetic plan"}`.
Use actual dates and zone IDs. PUT the draft URL plus `/{id}` to edit, including
`expectedRevision`. Publish at `/api/official/services/water/{id}/publish` with
`{"expectedRevision":1,"confirm":true}`. The `/retire` action uses the latest
revision. Missing confirmation/invalid inputs return 400; wrong service officials
403; stale state/revision or overlaps 409. Published rows cannot be edited in place.

Verification: four shared service cases passed (both Water and Waste), plus the
water HTTP authorization/CSRF/privacy/publication/revision test. A real Chrome
water workflow passed for draft creation/edit, hidden drafts, confirmed publication,
anonymous seven-day/source display and confirmed retirement back to Unknown.
