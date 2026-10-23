# NAG-037 — geographic inputs and source provenance

| Task | Owner | Acceptance |
|---|---|---|
| T-037.1 | Swapnil | Provenance/revision/audit schema, bounded Dhaka geometry validation, safe zone/metric writes, atomic curated transport catalog import. |
| T-037.2 | Redwan | Admin-only CSRF-protected data management/export/import; legacy vulnerability writes also record provenance. |
| T-037.3 | Rajiya | Geographic editor, source/sample labels, audit history, catalog export/import UI and map provenance display. |
| T-037.4 | Labiba | Bad geometry, stale edits/imports, audit/invalidation, authorization/CSRF and browser verification. |

Sources are user-supplied references, not independent verification or calibration.
Sample flags are explicit; missing legacy provenance remains Unknown. Boundary
edits preserve canonical IDs/codes and invalidate risk/weather inputs for the old
geography. Vulnerability edits invalidate old risk until evaluation. Audit snapshots
retain input sources and revisions. No observed accuracy or calibrated probability
is invented; real survey/labelled validation data is an external acceptance need.

Transport export/import is a bounded full enabled catalog update. IDs are stable;
omitted stops/links are disabled, never deleted (occupancy references remain valid).
Include every desired stop/link, their provenance/sample flags and fare rules.
An expected revision prevents stale imports, and validation occurs before writes.
Root source records the import reference; row sources identify individual data.
