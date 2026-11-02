# NAG-013 / NAG-014 — noise and construction concerns

Dedicated structured concerns use canonical Dhaka coordinates, a landmark/address,
observed time, subtype and description. Noise can optionally include an unverified
resident-reported decibel value; missing is never zero. Construction concerns are
suspected issues for inspection, not an automatic finding of a legal violation.
Optional reference text is supplied by the resident, not permit verification.

Submission is resident-only, idempotent per form token and private to the reporter
and provisioned city officials/admins. Other residents receive 404. Officials move
Pending to UnderReview/Rejected, and UnderReview to Resolved/Rejected with a
mandatory note. Revision checks prevent stale reviews; review history is retained.
Resolved is an attributed official disposition, not independent proof of remediation.
Observations must be within 30 days and ten new concerns per resident/hour are
allowed. These cases have dedicated tracking/review pages rather than general
crew-dispatch reports. No ML, audio inference, automated permit lookup or automatic
compliance/legal conclusion is claimed.

| Task | Owner | Acceptance |
|---|---|---|
| T-013.1 | Swapnil | Shared concern/review schema, bounded validation, private idempotent submission and revision-checked review history. |
| T-013.2 | Redwan | Noise resident and official APIs with CSRF, actual-role checks and safe DTOs. |
| T-013.3 | Rajiya | Noise submission/tracking/detail and official queue/review pages. |
| T-013.4 | Labiba | Privacy, idempotency, review transitions and browser verification. |
| T-014.1 | Redwan | Construction concern API and city review boundaries with dedicated types. |
| T-014.2 | Rajiya | Construction submission/tracking/inspection review navigation. |
| T-014.3 | Labiba | Construction-specific validation, privacy and end-to-end acceptance. |
