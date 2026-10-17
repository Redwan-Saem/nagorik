# Utility anomaly schema (T-009.1)

Report now includes optional utility metadata while retaining the existing coordinates, category/status fields, owner relationship, and single-photo ImageUrl. Existing reports do not acquire a guessed anomaly type, utility provider, or reference number.

| Field | Storage and meaning |
|---|---|
| AnomalyType | Nullable enum stored as a string: ExposedWire, GasLeak, FallenPole, PowerOutage, WaterLeak, Other. |
| Utility | Nullable UtilityProvider enum stored as a string: Unassigned, Desco, Dpdc, WaterAuthority, GasAuthority. |
| ReferenceNo | Nullable for older/general reports; unique when present. Format: UA- followed by at least six ASCII digits, maximum total length 32 characters. |
| PhotoPaths | Required ordered JSON array of zero to three nonblank strings, mapped as an EF primitive List<string> collection with in-place change tracking. Defaults to an empty array. |
| Latitude / Longitude | Existing double columns, reused without adding or renaming coordinate fields. |

Unassigned represents a routing no-match result; null means utility metadata has not been recorded. Providers are routing results, not resident-selected authority claims. WaterAuthority and GasAuthority are generic identifiers; this schema does not define service coverage or geographic boundaries.

## Risk mapping

UtilityAnomalyRules provides the server-side classification. ExposedWire, GasLeak, and FallenPole map to High urgency. PowerOutage, WaterLeak, and Other map to Normal. Undefined enum values throw rather than silently becoming normal-risk reports.

A database check also prevents the three high-risk types from being saved with Low or Normal urgency. High and Critical are accepted, allowing escalation. Reports with no anomaly type retain their previous urgency. The submission service will call this mapping in T-009.4.

## Constraints and indexes

SQLite checks reject undefined anomaly/provider names, malformed references, invalid photo JSON, non-array values, more than three paths, and null/non-string/blank array entries. CASE guards JSON functions against malformed input. Upload signatures, size, allowed paths, cleanup, and authorized file access belong to T-009.3. Zero photos remain valid at this schema layer for older/general records.

The filtered unique IX_Reports_ReferenceNo index permits multiple null legacy references and rejects duplicate non-null references. The schema also adds IX_Reports_AnomalyType_Latitude_Longitude_CreatedAt for later anomaly/location lookups and IX_Reports_Utility_CreatedAt_Id for provider-specific lists. Existing report filter indexes remain.

Reference allocation and concurrency-safe UA numbering belong to T-009.4. Its submission flow will also introduce the Utility Anomaly dashboard category; this task preserves the current category enum and APIs. Routing rules belong to T-009.2.

## Migration and compatibility

Migration: 20261016221457_AddUtilityAnomalySchema.

The migration adds four columns. It retains ImageUrl unchanged and copies each existing nonblank ImageUrl into a one-element PhotoPaths array before adding constraints. Reports without a photo receive an empty array. Existing coordinates, status, urgency, area, crew assignment, history, and report IDs are preserved.

New general reports created by the existing single-photo flow continue to use ImageUrl. The migration is a one-time backfill, not automatic synchronization between the two fields. Future multi-photo submission must populate PhotoPaths explicitly and retain a compatible primary ImageUrl where existing screens require it.

Apply from the API project directory:

    dotnet ef database update

Rollback to the preceding schema:

    dotnet ef database update 20261016211342_AddReportAreaUrgencyAndFilterIndexes

Rollback removes the new metadata columns and indexes. Original report fields, single-photo URLs, and relationships remain, but metadata stored only in removed columns is lost. Reapplying restores the legacy photo backfill; it cannot reconstruct removed anomaly/reference/multi-photo metadata.

## Verification

The task adds 45 focused tests covering every anomaly/provider value, risk classification, unknown enums, minimum high-risk urgency, Critical escalation, canonical/unique references, nullable defaults, photo JSON constraints, and edited photo lists.

The migration test starts at the preceding real schema with three legacy reports, a photo, coordinates, an assigned crew, status history, and assignment history. It verifies upgrade/backfill, index uniqueness, existing official-list access, rollback/reapply, no pending model changes, and a clean PRAGMA foreign_key_check. Tests use isolated databases; synthetic anomaly fixtures are not inserted into the normal application database.
