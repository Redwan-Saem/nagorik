# NAG-038 — external device and calibration acceptance

Software/browser fixtures do not prove physical GPS accuracy, a public push
service's delivery or calibrated flood accuracy. These items remain explicitly
unverified until observed with the relevant device/configuration/data.

## Physical GPS

Use HTTPS (or localhost on the same machine), OS Location services and a browser
with permission enabled. A phone's `localhost` refers to the phone, not this PC;
use a correctly hosted HTTPS development endpoint for another device.
On a real device, capture a known Dhaka location, record latitude/longitude,
reported accuracy, device/browser and time, compare against a known reference,
then test denial and manual address fallback. For SOS verify owner/responder
updates, throttling, stopping on page exit and no tracking after completion/cancel.
Do not record precise personal coordinates in public documentation.

The browser tests inject deterministic coordinates and verify flow/privacy; they
cannot prove the device provider can obtain a fix. “GPS unavailable” can be a
device/provider condition even when the app's manual flow is working.

## External push

Configure Vapid Subject/PublicKey/PrivateKey securely, use a deliberate dedicated
test account/device, grant permission and save a real subscription. Confirm the
device endpoint is yours before intentionally triggering an external notification.
Record consent, trigger, attempt/delivery state, device receipt and target-link
behavior without publishing private keys/endpoints. Test global/area opt-out and
stale/expired subscription handling. Mocked delivery counts are not proof of receipt.

Local startup smoke runs disable risk-alert delivery. No external push or emergency
message is sent as part of the automated completion check.

## Data/calibration

Import sourced operational boundaries/transport catalogs and have qualified staff
review them. Record survey method/date and source provenance. Rule-risk thresholds
remain an uncalibrated baseline until a separate labelled, representative dataset
supports an observed validation result. No percentage accuracy is claimed or
substituted from synthetic samples. NAG-034 ML and its dependent work stay excluded.
