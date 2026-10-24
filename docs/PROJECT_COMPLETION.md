# Remaining project completion

Scope approved: all remaining non-ML backlog features, integration and validation.
NAG-034 remains excluded. Existing historical sprint records are preserved.

## Delivery order

1. NAG-039: canonical risk routes, navigation and retirement of legacy seed readers.
2. NAG-016 / NAG-015: official loadshedding generation/publication and resident schedules.
3. NAG-018 / NAG-019: route/fare planning and recent bus occupancy.
4. NAG-010 / NAG-011: managed water and waste schedules.
5. NAG-012: air-quality provider integration and resident view.
6. NAG-013 / NAG-014: dedicated noise and construction review workflows.
7. NAG-037: geographic input management with provenance and truthful sample labels.
8. Older acceptance gaps, SOS local fixes, regression, release integration and documentation.
9. NAG-038: actual GPS / external push acceptance where supported hardware/config permits.

Each task is tested and committed/pushed separately with its assigned owner's author
and committer metadata. New task ownership follows schema/engine: Swapnil, API:
Redwan, provider/validation: Labiba, UI: Rajiya. Existing planned tasks keep their
workbook owners. Dates/effort are not invented. External calibration, observed
accuracy, provider availability and physical device success require real evidence.

## T-039.1 — canonical risk navigation (Redwan)

Acceptance: old risk API uses canonical Zones, public map compatibility does not
expose factors, legacy static pages move to the current map, old prediction IDs
are never reinterpreted as canonical zone IDs, and login lands on a valid local
role destination. Production startup no longer inserts fictitious legacy risk.

Verification: 51 focused API/navigation tests passed. No existing legacy tables
were deleted. Old prediction links open the canonical map without mapping legacy
numeric IDs to unrelated canonical zones. Generated workbook outputs are excluded
from application compilation/content discovery.

## NAG-016 — official loadshedding workflow

All eight planned tasks are implemented. Official generation, revision-checked
editing, validation warnings, confirmed publication, partial supersession and
audit are covered by 738 passing backend regressions and desktop/mobile browser
scenarios. See `NAG-016-loadshedding.md` for acceptance, routes, account and caveats.

## NAG-015 — resident loadshedding schedules

All six planned tasks are implemented: published-only seven-day queries and API,
local GPS polygon matching with manual selection, resident navigation and cards,
30-second refresh, and an explicitly labelled offline plan with safe shell caching.
Six query tests, eight focused API tests, 155 working-checkout client tests and two
desktop/mobile resident browser scenarios passed. The browser scenarios include
draft isolation, automatic publication refresh and a full offline reload. See
`NAG-015-resident-schedules.md` for routes, acceptance and offline/GPS limitations.

## NAG-018 — transport routes and fares

All eight tasks are implemented: a directed catalog and migration, bounded route
search, configurable per-leg fares, sorting/detail API, local landmark/GPS/map
selection and responsive result/detail UI. Seven backend tests, eight client tests
and two desktop/mobile browser scenarios passed. See `NAG-018-transport.md` for
exact bounds, demo/API steps and limitations. Development catalog routes/fares
are explicitly synthetic, and the local provider does not geocode arbitrary streets.

## NAG-019 — recent community bus occupancy

All four tasks are implemented: atomic cooldown/current-report schema, safe public
read and resident submission API, occupancy screen, and verification. Three backend
tests and one mobile Chrome workflow passed, including CSRF, privacy and expiry.
See `NAG-019-bus-occupancy.md`. Reports describe segments, not tracked buses.

## NAG-010 — managed water schedules

All four tasks are implemented: shared validated schedule model/migration, public
and official APIs, public weekly view and draft/edit/publish/retire forms. Four
shared service tests, one water HTTP test and a complete water browser workflow
passed. See `NAG-010-011-service-schedules.md` for roles, source limitations and demo.

## NAG-011 — managed waste schedules

All three tasks are implemented using the shared schedule model and city-specific
authorization. Waste API privacy/role checks passed, and both water/waste browser
workflows passed on the final run. Public pickup notes and real publication times
appear only after confirmed publication. No fake operational schedules are seeded.

## NAG-012 — air quality provider and view

All four tasks are implemented: validated Open-Meteo/CAMS integration, canonical
public API, source/freshness-aware view and verification. Three backend tests and
one real browser workflow passed. A separate live request confirmed the provider
schema. See `NAG-012-air-quality.md`; readings are regional model estimates, not
local sensor measurements. Provider outages remain truthful Unknown/Stale.

## NAG-013 — noise concerns and city review

All four tasks are implemented: private structured concerns, idempotent/transactional
submission, safe resident/official APIs and tracking/review pages. Six concern
backend cases and one real resident/official browser workflow passed. Review notes
and state changes are retained; optional decibels are unverified, not inferred.
See `NAG-013-014-civic-concerns.md` for API/demo and privacy boundaries.

## NAG-014 — construction concerns and inspection review

All three tasks are implemented using the private concern engine, construction
types and dedicated resident/city routes. Construction HTTP validation/privacy
passed; both Noise and Construction browser review workflows passed on the final
run. Reports remain suspected concerns and references remain unverified.

## NAG-037 — managed geographic and transport data

All four software tasks are implemented: validated provenance/revision/audit
infrastructure, protected admin writes/import/export, editor and map metadata,
and verification. Engine/API checks and managed-data/risk-admin browser journeys
passed. All 163 working-checkout client tests passed. See `NAG-037-managed-data.md`.
This enables recorded sources and curated data; it does not invent calibration
or survey accuracy evidence.

## NAG-040 — homepage and role workspace

All three tasks are implemented: public landing/role catalog, responsive cards
and mobile navigation, cookie sign-out and verification. Ten role checks and two
desktop/mobile browser cases passed, including real feature navigation and logout.
Screenshots were inspected. See `NAG-040-homepage.md`. Home no longer redirects.

## Final integration

The older SOS browser-fetch receiver error is corrected with regression coverage.
Login cookies use Secure outside local HTTP Development. Civic report photos now
use private storage, and legacy photo links require report ownership or authorized
staff access. See `NAG-039-integration.md` and `NAG-039-photo-privacy.md`.

`RUN_PROJECT.md` contains setup, seeded accounts, page URLs, API examples and demo
checks. `FINAL_VERIFICATION.md` records the final regression results and limits.
External GPS/push and calibration evidence still require real hardware,
configuration and data; the acceptance protocol is in
`NAG-038-external-acceptance.md`. NAG-034 ML and its dependent work remain excluded
as requested. Software implementation is distinct from external acceptance.
