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
