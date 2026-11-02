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
