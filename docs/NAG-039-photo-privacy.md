# T-039.5 — civic report photo privacy (Redwan)

General civic submissions now use private storage, as utility anomaly submissions
already did. The legacy storage method also delegates to private storage so future
callers cannot accidentally create new public report photos. Invalid numeric user
IDs are rejected before a file is written; failed database saves remove the newly
stored private photo.

Existing `/uploads/reports/{guid}.jpg|jpeg|png` files are preserved in place. Static
middleware excludes their physical directory, including path aliases. The original
URLs are handled by the authenticated photo controller, with the same report-owner,
assigned-crew, dispatcher and city-official access rules as private photo URLs.
Responses use private/no-store and nosniff headers. Unknown or unrelated photos
return 404; anonymous requests require authentication. Files are not migrated or
deleted by this change. Previously downloaded or externally cached copies cannot
be recalled.

The isolated HTTP fixture uses the production static-file filter. New regression
cases exercise actual civic multipart submission/private file retrieval and legacy
URLs for owner, another resident, anonymous visitor and dispatcher, including
anonymous requests with doubled separators and case variants. Existing utility
photo validation/ownership tests remain in the regression suite.
