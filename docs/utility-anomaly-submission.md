# Utility anomaly submission (T-009.4)

POST /api/reports/utility-anomalies accepts multipart/form-data from an authenticated Resident whose identity still belongs to a resident in the database. The resident ID comes from NameIdentifier; posted UserId, Category, Utility, Urgency and ReferenceNo fields are ignored.

Required fields:

- AnomalyType: ExposedWire, GasLeak, FallenPole, PowerOutage, WaterLeak or Other.
- Description: 10–1000 characters, also checked after trimming.
- AddressText: a nonblank address, up to 300 characters.
- AreaId: an existing positive report area identifier.
- Photos: one to three JPG/PNG files under the T-009.3 validation rules (at most 5 MiB each).

Latitude and Longitude are optional together, must be finite, and must lie within -90..90 and -180..180 respectively. Omitted coordinates use the existing report model's 0/0 sentinel; the routing service receives null coordinates so geographic rules cannot match an omitted location.

Example browser request, using an existing photo input and authenticated session:

    const form = new FormData();
    form.append('AnomalyType', 'GasLeak');
    form.append('Description', 'Gas pipe leaking near the street');
    form.append('AddressText', 'Street address in Dhaka');
    form.append('AreaId', '1');
    form.append('Photos', document.querySelector('input[type=file]').files[0]);
    const response = await fetch('/api/reports/utility-anomalies', {
        method: 'POST', credentials: 'same-origin', body: form
    });
    console.log(response.status, await response.json());

Success returns 201 with Location /api/reports/{id} and a body containing id, referenceNo, category, status, urgency, utility, routingOutcome and photoPaths. Reference format is UA-000123 (six digits minimum, expands for larger IDs). It uses the database-allocated report primary key, not an application counter or MAX()+1. References can have gaps because other report categories share the ID sequence. The database's unique reference index remains the final constraint.

Category is UtilityAnomaly, displayed as Utility Anomaly in official filters and table rows. Status starts Pending. ExposedWire, GasLeak and FallenPole get High urgency; other types get Normal. T-009.2 rules choose the provider; no-match or ambiguous routing keeps Unassigned for review. DESCO/DPDC map to the existing DpdcDesco authority; other providers use Other rather than incorrectly routing to CityCorporation. Utility retains the specific provider identity.

Private photo storage wraps a database transaction containing report insertion, reference allocation and initial Pending history. An update/history/commit failure rolls back database rows and removes newly stored photos. Existing process-crash and cleanup limitations of T-009.3 still apply. No notification is sent on initial submission. Rate limits and duplicate warnings belong to T-009.5.

Invalid form or photo input returns 400 validation details; unauthenticated/invalid resident identities return 401 and nonresident roles return 403. The request envelope limit is 17,000,000 bytes to fit three 5 MiB photos plus form overhead. The generic report endpoint refuses UtilityAnomaly to prevent bypassing this contract. The existing generic report form excludes that category until the utility form task.

No migration is needed: categories already persist as strings without a category check constraint, and T-009.1 supplied the utility columns, JSON photo paths and unique reference index.

UtilityAnomalySubmissionApiTests runs real HTTP requests against JWT authentication and file-backed SQLite. It covers all anomaly types, required-field and description boundaries, invalid coordinates/photos, ownership and role enforcement, server-controlled fields, official dashboard inclusion, rollback after reference-save failure, and eight concurrent submissions with distinct references.
