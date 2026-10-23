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

## Run and demonstrate

After `dotnet ef database update`, start the application and sign in as
`admin1@test.com` (initial `Test@12345`; existing passwords are not reset).
Open `/admin/data`. Create a distinct demo zone with a stable code such as DEMO1,
an explicitly labelled demonstration source and the Sample checkbox. Example
geometry: `{"type":"Polygon","coordinates":[[[90.44,23.74],[90.45,23.74],[90.45,23.75],[90.44,23.75],[90.44,23.74]]]}`.
Unknown metrics can stay empty. Inspect the resulting audit entry and map popup.
The editor validates coordinates/rings/holes; it does not certify surveyed accuracy
or perform a complete GIS topology review of interactions between multipart zones.

Existing zone codes cannot be renamed. Edited boundaries clear old cached rainfall;
metrics clear old risk. Risk remains Unknown until current inputs are collected and
evaluated. Source changes and revisions are recorded; historical risk explanations
retain the input source/revision used at computation. Legacy metric API writes
without source are recorded as unverified legacy manual input, not verified data.

For transport, export the full enabled catalog from `/admin/data/transport/export`.
Set its root source and review all row sources, sample flags, fares, distances and
directed stop IDs before importing the edited JSON. Keep expectedRevision current.
New production installations can import curated operating data without enabling
synthetic Development seeding. Import is atomic; stale revisions fail. It cannot
independently verify operator timetables, permits, source authenticity or fare claims.

Admin APIs under `/api/admin/data`: GET zones, transport, changes (latest 100
metadata rows), and changes/{id} (one full snapshot). Obtain security-token with
the same cookie jar before POST zones/transport or PUT zones/{id}; send the token
as RequestVerificationToken. All routes require Admin; public map routes expose
only public source/sample metadata, not audit identities or snapshots.

Verification: three engine cases and admin HTTP/CSRF/stale-edit/import checks
passed. Existing threshold/vulnerability HTTP and browser workflows passed.
The managed-data Chrome scenario passed for invalid input retention, sourced zone
creation, catalog import/audit and public map provenance. Its first map click was
intercepted by an overlapping fixture; the fixture was moved and passed with the
same timeout. All 163 working-checkout client tests passed, including safe semantic
factor/source rendering. No real calibration/accuracy evidence is claimed.
