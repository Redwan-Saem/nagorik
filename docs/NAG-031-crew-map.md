# NAG-031 — crew locations

Acceptance criteria: `NON_ML_ACCEPTANCE_CRITERIA.md`.

T-031.1 (Swapnil): nullable crew latitude/longitude/last-seen fields and migration;
database check requires all three together and validates ranges. Existing crews
remain unknown. No task/report coordinate is copied into a crew's GPS location.

T-031.2 (Labiba): task detail has explicit capture/skip buttons; denied GPS does
not block status changes. Optional coordinates are validated and saved with the
owned report transition in the same SaveChanges transaction. Conflict rolls both
back. A status update without GPS preserves the earlier location timestamp.
