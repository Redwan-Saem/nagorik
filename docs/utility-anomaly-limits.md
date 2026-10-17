# Utility anomaly submission limits (T-009.5)

The existing POST /api/reports/utility-anomalies permits ten accepted utility anomaly reports per authenticated resident in a rolling UTC hour. It counts persisted UtilityAnomaly reports with CreatedAt in (now - 1 hour, now], regardless of their current status. Failed requests, other users and general reports do not consume this allowance.

The eleventh accepted-report attempt returns HTTP 429 with an integer Retry-After header and JSON message/retryAfterSeconds. The delay is rounded up to whole seconds until enough reports leave the window. A report exactly one hour old has expired from the quota. The check runs inside the existing immediate SQLite write transaction, before report insertion, so separate connections/processes using this endpoint cannot race past the quota. This uses persisted database state rather than an in-memory counter. Photos are validated/stored before the transaction; rate rejection rolls back and compensates those new files through T-009.3 cleanup.

Duplicates warn and are still accepted with HTTP 201, consuming one accepted submission. The response adds isPossibleDuplicate, duplicateOfReportId and warning. The existing report duplicate fields are persisted. A duplicate requires the same authenticated owner, anomaly type, area ID and normalized address, with CreatedAt in (now - 24 hours, now]. The most recent match wins, breaking timestamp ties by report ID. A report exactly 24 hours old is excluded.

Address normalization uses Unicode compatibility normalization (FormKC), collapses whitespace, trims, and compares invariant uppercase. GPS coordinates are not part of this matching rule; GPS-only proximity with different address text is not detected. Different areas remain separate even when address text matches. Future-dated records are excluded from both windows. The generic report duplicate logic is unchanged.

TimeProvider.System supplies production time; the HTTP tests inject a controllable clock for exact boundaries. No migration or configuration change is required.

Verification extends UtilityAnomalySubmissionApiTests: first ten accepted/eleventh rejected, Retry-After, exact hour expiry, 24-hour edges, normalization, separate owners/types/areas/addresses, failed request allowance, persisted flags, and twelve concurrent requests across separate SQLite connections yielding exactly ten successful reports and two 429 responses with no leftover photos or extra history rows.
