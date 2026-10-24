# NAG-039 — integration corrections and final verification

T-039.1 (Redwan) retired canonical-risk navigation/seed-reader inconsistencies;
its original owner-attributed commit is retained.

T-039.2 (Labiba): SOS submission and live-location modules call browser fetch
through a global wrapper. A stored unbound browser function could otherwise throw
`Failed to execute fetch on Window: Illegal invocation` when invoked as a class
member. Regression cases exercise the default browser path, not only an injected
mock function. All 163 working-checkout client tests passed, including both new
receiver tests. GPS provider denial/unavailability remains a separate condition;
manual addresses are supported and no physical GPS accuracy is claimed.

T-039.3 (Redwan): login cookies are HttpOnly/SameSite=Lax and Secure on HTTPS or
outside Development. Local HTTP Development still works. Expiry uses UTC. Four
actual login-header cases passed for HTTP/HTTPS Development and Production/Staging.
Production must be served over HTTPS. The HTTP UI fixture explicitly uses Development.

T-039.5 (Redwan): civic photo uploads use private storage. Existing public-folder
URLs now require the same authorization as private photos; static middleware cannot
serve that directory. Files/URLs are preserved. See `NAG-039-photo-privacy.md`.

T-039.4 (Labiba): final integration removes the empty placeholder test and unused
external-server UI base. The service-worker browser check now owns an isolated
server and verifies public-shell caching without caching API responses. Mobile
dispatcher navigation opens/closes its menu explicitly, and real marker clicks
wait for cluster-animation hit testing. No production assertion is weakened.
Generated outputs/test results and local backups are ignored; existing personal
demo/study files are preserved. A duplicate API import is removed.

`FINAL_VERIFICATION.md` records regression, migration and live startup results;
`RUN_PROJECT.md` provides the local run/demo checklist. Missing external delivery,
physical GPS or calibration evidence is not represented as verified acceptance.
