# NAG-035 — My areas and canonical risk notifications

See `NON_ML_ACCEPTANCE_CRITERIA.md`. This story depends on canonical rule-based
risk and the resident map, not ML.

T-035.1 (Swapnil): canonical resident-zone subscriptions have enabled state,
creation/update timestamps, unique resident-zone membership and restrictive
foreign keys. The migration adds a table; legacy AreaSubscriptions remain intact.
