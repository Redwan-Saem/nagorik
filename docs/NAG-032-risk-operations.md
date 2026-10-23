# NAG-032 — operations driven by rule risk

See `NON_ML_ACCEPTANCE_CRITERIA.md`.

T-032.1 (Labiba): a transition into High/Severe appends a durable alert event in
the same transaction as risk history. An independent minute worker uses the
existing notification service and opted-in Resident area subscriptions. Unchanged
levels produce no event. Canonical rules are not copied into legacy seeded risk
tables. Per-recipient deduplication/cooldown is completed in T-035.5.

T-032.2 (Redwan): Dispatcher-only `/api/map/flagged-drainage` lists points in
High/Severe zones evaluated within 15 minutes, capped at 5000. It includes point
status, coordinates, zone and evaluation time. Clear points remain eligible because
the flag describes zone risk. The drainage GeoJSON also exposes `isFlagged`.

T-032.3 (Rajiya): `/dispatcher/flagged-drainage` lists up to 100 flagged points
and busy/free crews. Quick assignment creates an Approved inspection report and
uses the existing assignment/history/notification workflow. A concurrency-token
work link prevents competing point requests from creating duplicate reports.
Existing active point work is rejected, not reassigned silently. Busy crews need
explicit confirmation. If assignment fails after allocation, the linked Approved
report is reused on retry. Completed/rejected work allows a new inspection.
All mutations require Dispatcher identity and antiforgery tokens.
