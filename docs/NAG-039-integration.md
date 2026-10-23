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

Final backend/browser regression, migration checks and live local startup results
are recorded after execution. Missing external delivery/calibration evidence must
not be represented as verified acceptance.
