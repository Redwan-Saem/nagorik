# NAG-033 — public/resident map

See `NON_ML_ACCEPTANCE_CRITERIA.md`.

T-033.1 (Redwan): anonymous GeoJSON endpoints `/api/public/map/risk-zones`,
`reports`, `blockages`. Reports default to private/unverified, independently of
workflow approval. Only explicitly verified usable coordinates appear. Public
report properties contain generated reference/category/status/time, not free-text
title/address/description, author, reviewer, crew, photo or contact information.
Blockages include Blocked points only. Public endpoints never reuse crew DTOs.
Verification columns/migration are foundational here; review controls are T-033.4.

T-033.2 (Rajiya): `/map` is available anonymously and from the shared navigation.
It uses local Leaflet, OSM background tiles, independent layer toggles, risk-color
legend, safe text popups and per-layer empty/error/refresh feedback. Public fetches
omit credentials. `?zone=ID` focuses the map on a canonical zone. Sample boundaries
and uncalibrated assessments are labelled explicitly. No dispatcher scripts load.

T-033.3 (Rajiya): Locate me is opt-in, uses standard longitude/latitude Polygon
and MultiPolygon containment with holes, and displays all matching zone risks and
evaluation times. GPS coordinates stay in this page; no application API receives
them. Clear cancels pending callbacks and removes the marker. Denial, timeout,
unloaded zones and outside-boundary locations have explicit feedback. OSM tiles
are requested for the viewed map area, as with ordinary map navigation.

T-033.4 (Labiba): `/dispatcher/report-verification` supports explicit verify/hide
decisions with reviewer/time and a monotonically increasing revision. Role checks,
CSRF and an atomic expected-revision update prevent unauthorized and stale writes.
Publishing requires usable coordinates. The page explains which fields become
public. Workflow approval/completion is independent from publication.
