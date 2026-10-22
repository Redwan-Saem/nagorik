# NAG-033 — public/resident map

See `NON_ML_ACCEPTANCE_CRITERIA.md`.

T-033.1 (Redwan): anonymous GeoJSON endpoints `/api/public/map/risk-zones`,
`reports`, `blockages`. Reports default to private/unverified, independently of
workflow approval. Only explicitly verified usable coordinates appear. Public
report properties contain generated reference/category/status/time, not free-text
title/address/description, author, reviewer, crew, photo or contact information.
Blockages include Blocked points only. Public endpoints never reuse crew DTOs.
Verification columns/migration are foundational here; review controls are T-033.4.
