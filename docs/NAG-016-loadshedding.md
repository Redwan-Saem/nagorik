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
