# SOS API

The authenticated SOS screen is `/sos`. It redirects to `/sos/active` when an alert is already active. A two-second pointer/keyboard hold followed by release opens a review dialog; an alternative Review button supports users who cannot hold. Only the separate confirmation sends the alert. GPS permission denial leaves the manual address path available. The saved-alert screen is `/sos/{id}`, restricted to its owner, and offers an explicit cancellation confirmation. Cancellation stops this page's GPS watch before submitting and shows the saved terminal status after success.

All endpoints require authentication using the existing `auth_token` cookie (or a valid JWT bearer token). Alert ownership comes from the authenticated user; caller-supplied owner, responder, and status fields are ignored.

| Method | Route | Behavior |
|---|---|---|
| POST | `/api/sos` | Create an alert and Triggered event; return 201 and a Location header. |
| GET | `/api/sos/active` | Return the user's Active/Responding alert, or 204 if none exists. |
| GET | `/api/sos/{id}` | Read an owned alert, including after cancellation. |
| PATCH | `/api/sos/{id}/location` | Replace the location of an Active/Responding alert and append a location event. |
| POST | `/api/sos/{id}/cancel` | Cancel an Active/Responding alert and append a cancellation event; no body required. |

Trigger and location-update bodies accept:

```json
{"latitude":23.81,"longitude":90.41,"addressText":"Dhanmondi, Dhaka"}
```

For manual fallback, omit both coordinates and supply a nonblank `addressText`. Both coordinates must be supplied together, must be finite, and must be within latitude [-90, 90] and longitude [-180, 180]. Address text is limited to 500 characters. Location updates replace the full location, so include the address again if it should be retained alongside new coordinates.

Responses contain `id`, `latitude`, `longitude`, `addressText`, `createdAtUtc`, `updatedAtUtc`, `status`, and `responderUserId`. Times are UTC and status names are strings.

Invalid bodies return 400, missing authentication returns 401, and missing or other users' alerts return 404. A second active alert returns 409 with `activeAlertId` in the problem response. Terminal alerts and concurrent stale writes return 409; read the alert again before retrying.

Repeated cancellation returns 200 without another event. Repeating an identical location update also returns 200 without another event. Triggering again while an alert is active returns 409 and never creates a second alert. Each successful write commits the alert and audit snapshot together.

## Live location and responder notifications (T-026.3)

The owner can open `/sos/active`, enable GPS sharing, or update a manual address. GPS sharing verifies the alert is active before starting a watch, sends at most once every 10 seconds, and suppresses movement under 25 meters until 30 seconds have elapsed. It polls status every 15 seconds and stops on cancellation/completion, explicit stop, page exit, permission denial, or an authorization/conflict response. Tracking requires browser geolocation support and an open page; it does not run after the page closes.

Configure `Sos:ResponderEmails` with the existing account emails authorized to receive and view SOS alerts. The default list is empty: no crew, dispatcher, or other user is selected implicitly. For example, set `Sos__ResponderEmails__0` in the server environment to the first designated responder's email. Matching ignores case and whitespace. No emergency service integration is implied by this mapping.

After a new alert and event are saved, the server immediately attempts push delivery to the configured accounts, with an independent notification scope and timeout for each recipient. `Sos:NotificationTimeoutSeconds` defaults to 15 (valid range 1–30). Responders need an existing browser push subscription and valid server VAPID configuration. Notification links open `/sos/respond/{id}`, accessible only to configured responder accounts. This page displays current status and location when refreshed.

Server logs record creation-to-dispatch latency for each responder, flag dispatches starting after 60 seconds, and report missing recipients, zero delivered devices, timeouts, or exceptions. Alert creation is preserved when delivery fails. The 60-second target is tested with a controlled push service; actual device receipt depends on push configuration, connectivity, and the browser. Delivery failures are logged; there is no automatic server-side retry in this task.
