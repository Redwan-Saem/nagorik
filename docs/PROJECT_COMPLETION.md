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
