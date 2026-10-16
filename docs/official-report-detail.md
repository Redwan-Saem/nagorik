# Official report detail (T-008.3)

`GET /api/official/reports/{id}` accepts `Official` or `CityOfficial` through the same role rule as the official report list. It does not broaden the resident-only `GET /api/reports/{id}`, which still checks the report owner.

The response contains:

- `id`, `code`, `title`, `description`, `category`, `authority`, `status`, `urgency`, and `createdAt`.
- `areaId`, `areaName`, `location`, `latitude`, and `longitude`.
- `imageUrl`: the existing report photo URL, or null for a missing/blank URL.
- `assignedCrewId` and `assignedCrewName`, both nullable.
- `statusHistory`: an array of `status`, nullable `previousStatus`, nullable `changedByUserId`, and `changedAtUtc`.

History is oldest first, with history ID breaking timestamp ties. Legacy entries may have no recorded actor or previous status. A report without history returns an empty array. Unclassified areas and absent crew assignments return null fields. The endpoint projects report and history data without serializing EF navigation graphs or account contact/password fields.

Responses are not cached. Anonymous requests return 401, other authenticated roles return 403, an unknown positive report ID returns 404, and zero/negative IDs return 400. All queries are read-only and accept request cancellation.

## Official detail screen (T-008.6)

`/official/reports/{id}` renders the same projected detail data for officials and the CityOfficial alias. Select a report's code with keyboard or pointer, or click a non-interactive part of its table row. The link carries the applied list query in `returnUrl`; **Back to city reports** restores the categories, area, page, and sorting. Browser Back remains available. External and unrelated return URLs fall back to the official report list.

The page shows description, location and coordinates, the existing uploaded photo, area, urgency, authority, assigned crew, and ordered UTC status history. Missing photos/history and unassigned crew have explicit states. Uploaded-photo failures show a fallback message; only local `/uploads/` photo paths are displayed. Razor encodes report text. Anonymous requests return 401, other roles see a 403 permission page without querying report data, and missing/invalid report IDs show a 404 page with the preserved Back link. Refresh and direct links load through the same server authorization checks.
