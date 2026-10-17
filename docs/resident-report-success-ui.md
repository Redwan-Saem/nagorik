# Success and My Reports UI (T-009.8)

The existing /Reports/Confirmation/{id} success page reads the owned report from the database on every visit. It displays the exact persisted UA reference (or the legacy R code), current resident status, retained duplicate warning, and links to My Reports, details and another submission. Utility submissions link back to the utility form. Reloads and deep links do not depend on sessionStorage, query-string references or a previous POST response.

My Reports at /Reports/Mine now uses the same ResidentReportQueries projection as GET /api/reports/mine: owner filtering, newest-first ordering with ID ties, twenty reports per page and consistent resident labels. It includes both utility and legacy reports, empty/out-of-range page messages, pagination links and existing photo thumbnails. Photo URLs render only from approved local upload namespaces. The API list adds the imageUrl property to support the shared presentation projection.

Details and status history also show the persisted UA reference and public labels. Pending is Submitted, Approved/Assigned are Acknowledged, InProgress is In Progress, Completed is Resolved and Rejected stays Rejected. Generic reports keep their R code.

GET /reports/reference/{reference} provides a reference deep link. It validates UA format and maximum length, looks up the reference within the authenticated owner's reports, and redirects to the existing detail page. Unknown/malformed references and another resident's reference return 404. Resident-only authorization applies to all these pages, with no-store caching for private report content. My Reports also verifies an existing resident identity and validates pagination.

The table has scoped headers, an accessible caption and a keyboard-focusable horizontal scrolling region for narrow screens. Actions wrap and references use selectable text. Razor encodes titles and other report text. No new JavaScript or migration is needed.

HTTP tests cover all six labels across confirmation/list/detail/reference links, exact reference and navigation URLs, reload/deep-link behavior, owner isolation, role enforcement, safe missing resources, encoded report titles, legacy codes, empty states and pagination. Existing API pagination and status tests continue to run against the shared query.
