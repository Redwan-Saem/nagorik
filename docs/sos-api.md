# SOS API


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
