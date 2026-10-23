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

## Run and verify

Stop the app, run `dotnet ef database update`, then `dotnet run --launch-profile http`.
Open `/transport/occupancy` publicly and choose a segment: without recent reports
it displays Unknown. Sign in as `resident1@test.com` (initial `Test@12345`), select
Seats available, Standing passengers or Full and submit. Repeat immediately to
see the five-minute cooldown. Open the public page in another session to see the
summary without reporter identity. Existing account passwords are not reset.

Public API: `GET /api/transport/occupancy/segments`, then
`GET /api/transport/occupancy?linkId=<id>`. For authenticated resident clients,
obtain `/api/transport/occupancy/security-token`, send its token as
`RequestVerificationToken` with the same cookie jar and POST JSON
`{"linkId":1,"level":"Full"}` to `/api/transport/occupancy`. Use a real segment ID.
Invalid input is 400, missing/disabled/non-bus segment 404, other roles 403,
anonymous submission 401 and cooldown 429. Public data contains no resident IDs.

Three backend tests passed for cooldown/update/freshness, role/bus validation,
HTTP authorization, CSRF and public privacy. A mobile Chrome workflow passed
for public Unknown, real resident sign-in/submission, repeat rejection, anonymous
read, expiry back to Unknown and no horizontal overflow. The service polls every
30 seconds while visible and clears unverified old display on request failure.
