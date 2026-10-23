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

## Noise demonstration and API

After `dotnet ef database update`, start the app. As `resident1@test.com` (initial
`Test@12345`), open `/resident/concerns/noise`, choose a source type, describe the
observation, add a landmark and a recent Dhaka local time. Optional GPS fills
coordinates; manual fixture coordinates `23.75, 90.37` work for a clearly labelled
demo concern. Leave decibels blank unless measured. Submit to obtain NC reference
and tracking detail. Your latest 50 concerns appear on the submission page.

As `official1@test.com`, open `/official/concerns/noise`, select the concern, add
an investigation note, then record a resolved/rejected disposition. The resident
detail shows the timestamped history. Do not present these demo notes as an actual
inspection. Existing account passwords are not reset.

Resident JSON clients use `/api/resident/concerns/security-token` with their cookie
jar, then `RequestVerificationToken` for POST `/api/resident/concerns/noise`:
`{"subtype":"Commercial","description":"Observed amplified sound near the landmark.","address":"Demo landmark","latitude":23.75,"longitude":90.37,"observedAtUtc":"2026-10-23T16:00:00Z","submissionToken":"<new GUID>"}`.
Use a current observation time and retain the same GUID/content when retrying.
GET the list or `/{id}` on the resident route. City clients use
`/api/official/concerns/security-token`, then POST
`/api/official/concerns/noise/{id}/review` with
`{"expectedRevision":1,"state":"UnderReview","note":"Investigation opened by the city."}`.
Use the latest revision for the next transition. Anonymous access is 401, wrong
roles 403, other residents' IDs 404, invalid input/CSRF 400 and stale reviews or
changed-content retries 409. The hourly allowance returns 429; a replay of a saved
submission still succeeds. The rate check and insert share a database transaction.

Verification includes four initial shared cases (both concern types), the noise
HTTP privacy/idempotency/CSRF/review case and a real Chrome resident/official
investigation-to-resolution workflow. A fifth service case checks hourly allowance
and replay behavior. GPS hardware and optional decibel accuracy are not claimed.
