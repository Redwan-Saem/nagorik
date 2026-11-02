# NAG-019 — recent community bus occupancy

| Task | Owner | Acceptance |
|---|---|---|
| T-019.1 | Swapnil | Bus segment/resident foreign keys, unique current report per person/segment, 15-minute freshness, atomic five-minute update cooldown. |
| T-019.2 | Redwan | Public safe summary/catalog; resident-only CSRF-protected submission; validation and no reporter identities exposed. |
| T-019.3 | Rajiya | Segment picker, freshness/distribution/Unknown state, accessible report form and honest sample/service limitations. |
| T-019.4 | Labiba | Cooldown, freshness, authorization/CSRF and real browser verification; run/demo documentation. |

Reports describe a direction-specific catalog bus segment, not a tracked vehicle.
They are unverified community observations, not a measured real-time headcount.
The display uses the upper median category among distinct residents reporting in
the last 15 minutes and shows the full category distribution and latest timestamp.
No recent reports means Unknown, never Empty. One resident can update each segment
once per five minutes; the server timestamps submissions. Synthetic catalog labels
remain visible. Production requires an enabled curated bus catalog.
