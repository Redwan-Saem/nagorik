# Resident report list (T-009.6)

GET /api/reports/mine requires the Resident role and an authenticated NameIdentifier belonging to an existing resident. It returns only that resident's reports, including general and utility anomaly categories. A posted/query userId is not used. Anonymous/invalid identities receive 401; other roles receive 403.

The response contains items, page, pageSize (20), totalCount and totalPages. Optional page defaults to 1; invalid/nonpositive/overflowing pages receive 400. Empty results and pages beyond the last page have an empty items array. Reports sort by CreatedAt descending, then Id descending for stable ties. Responses disable caching. Queries are projected, read-only and bounded to one page; no report history or state is changed.

Each item contains id, code, referenceNo, title, category, anomalyType, utility, status, urgency, areaId, areaName, location and createdAt. Utility reports use their UA reference as code. General/legacy reports without a reference use the existing R-000123 style code and return null referenceNo; absent anomaly/provider metadata stays null. No resident identity, authentication fields, or report entities are serialized.

Public status labels:

| Internal state | Resident status |
| --- | --- |
| Pending | Submitted |
| Approved | Acknowledged |
| Assigned | Acknowledged |
| InProgress | In Progress |
| Completed | Resolved |
| Rejected | Rejected |

Rejected is distinct from Resolved and is never presented as successfully completed. The shared ResidentReportStatus mapping also adds displayStatus to the existing resident detail response and its statusHistory entries, preserving their existing internal status fields for compatibility. The MVC Mine view is unchanged; UI integration belongs to the following UI tasks.

Example with the existing authenticated cookie:

    const response = await fetch('/api/reports/mine?page=1', { credentials: 'same-origin' });
    console.log(response.status, await response.json());

The static mine route is distinct from the existing integer-constrained /api/reports/{id:int} route and the official GET /api/reports list. No migration or new service registration is required.

HTTP tests cover all six state mappings, owner isolation including forged query userId, resident-role validation, empty results, stable pagination, legacy/null metadata, page validation, read-only behavior, detail-label consistency and a newly submitted anomaly appearing as Submitted with its original reference.
