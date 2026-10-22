# NAG-032 — operations driven by rule risk

See `NON_ML_ACCEPTANCE_CRITERIA.md`.

T-032.1 (Labiba): a transition into High/Severe appends a durable alert event in
the same transaction as risk history. An independent minute worker uses the
existing notification service and opted-in Resident area subscriptions. Unchanged
levels produce no event. Canonical rules are not copied into legacy seeded risk
tables. Per-recipient deduplication/cooldown is completed in T-035.5.
