# NAG-016 — loadshedding generation and publication

All times in a schedule are local Asia/Dhaka civil times. Capacity and demand are
entered in MW by an authorized utility official. No live grid feed or actual outage
claim is implied. Existing canonical Zones are the selectable areas; no fictitious
demand is seeded into the working database.

## Tasks and acceptance criteria

| Task | Owner | Acceptance |
|---|---|---|
| T-016.1 | Redwan | Additive migration stores draft/published/superseded versions, per-zone demand snapshots, bounded outage slots, unique current zone/day publications and audit records; existing data is preserved and invalid time/capacity is rejected. |
| T-016.2 | Swapnil | Deterministic rotation respects supplied-capacity limit, balances outage counts, rotates equal candidates across days, produces no outages when capacity is sufficient, validates edited slots and generates up to 100 zones within five seconds. |
| T-016.3 | Labiba | Only utility official roles may generate/edit/publish; validate all finite positive demands/capacity, zone IDs, period and CSRF. Resident, crew and generic city officials cannot mutate schedules. |
| T-016.4 | Swapnil | Generate saves an auditable Draft; edit requires expected revision and revalidates grid/capacity/fairness; no draft is exposed to resident schedule queries. |
| T-016.5 | Redwan | Confirmed publication checks revision and validation, records publisher/time, supersedes replaced publications atomically, preserves unaffected zone/days, and audits every generate/edit/publish. |
| T-016.6 | Rajiya | Generator page provides actual zones, demand/capacity/period inputs and clear validation/results. |
| T-016.7 | Rajiya | Draft review supports slot editing, validation warnings and explicit publication confirmation. |
| T-016.8 | Labiba | Tests cover role matrix, validation, concurrency, capacity/fairness, publication isolation/overlap, audit and 100-zone performance. |

Whole-zone switching is discrete: removing enough demand to keep supplied load
within capacity may shed more than the exact shortfall. The earlier spreadsheet's
wording that shed load must never exceed shortfall cannot generally hold with
indivisible unequal-demand zones. The implementation must expose this fact and
require explicit warning acknowledgement, rather than publish an unsafe plan or
claim exact matching. Reviewed demand inputs and real operational approval remain
necessary before using any plan outside the demonstration.

## Run and demonstrate

With the app stopped, apply migrations with `dotnet ef database update`, then
start `dotnet run --launch-profile http`. Development provisions
`utility1@test.com` with initial password `Test@12345` (existing passwords are not
reset). Production utility accounts must be provisioned separately with
UtilityOfficial, DPDCOfficial or DESCOOfficial role. Generic Official/CityOfficial
accounts intentionally cannot manage electricity schedules.

Open `http://localhost:5000/official/loadshedding` after login. Choose zones and
enter their reviewed demand in MW, capacity and period. For a synthetic demo,
two zones at 10 MW each with 10 MW available for one day produces twelve
two-hour outage slots, alternating zones. The UI never claims these are actual
grid readings. Generate a draft, inspect demand snapshots and warnings, select a
slot to edit/save, then explicitly confirm publication and warning acceptance.
Reload to verify persistence and the read-only published state. Recent versions
and per-version audit history remain available. Slot tables show at most 100 rows
at once and support zone/date filtering. A new draft replaces only its zone/days
when published; old versions with no remaining current publications are superseded.

API routes: utility-only `/api/official/loadshedding` (GET list, POST generate),
`/{id}` (GET detail), `/{id}/slots` (PATCH), `/{id}/publish` (POST), `/{id}/audit`
(GET), `/zones` and `/security-token`. Mutations require the antiforgery cookie
and matching `RequestVerificationToken` header. Expected revisions reject stale
edits. Failed writes are not automatically replayed. Drafts with capacity errors
cannot be published. Official pages and API responses are no-store.

NAG-015 supplies the separate resident seven-day schedule view; it is not part
of this official generator story. No local working database was changed by tests.

## Completion evidence

All eight official-generator tasks are implemented. The final working checkout
passed 738 backend tests, 139 client tests (including two preserved local SOS
regressions outside this story), and two real Chrome generator/review/publication
scenarios at 1280px and 390px. EF reports no pending model changes. Tests cover
capacity safety, balanced rotation, 100-zone generation under five seconds,
CSRF/role boundaries, independently loaded stale contexts, publication refusal,
atomic partial/full replacement, audit and persisted read-only publication.

T-016.1 through T-016.4 were integrated into main in PR #79. T-016.5 through
T-016.8 retain separate owner-attributed commits and branches. The final story
PR targets main directly and includes these remaining tasks so the integrated
story can be reviewed without relying on merges between dependency branches.
