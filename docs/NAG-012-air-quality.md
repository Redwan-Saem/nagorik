# NAG-012 — air quality

| Task | Owner | Acceptance |
|---|---|---|
| T-012.1 | Labiba | Bounded provider integration, strict units/value/time parsing, cache/failure freshness and truthful Unknown. |
| T-012.2 | Redwan | Public canonical-zone API; no arbitrary upstream URLs or reporter/location data. |
| T-012.3 | Rajiya | Area picker, AQI band and PM values with source, model/freshness timestamps, Unknown and retry states. |
| T-012.4 | Labiba | Provider failure/parser/cache and API/browser tests; live schema check and demo documentation. |

Provider: [Open-Meteo Air Quality API](https://open-meteo.com/en/docs/air-quality-api),
using Copernicus CAMS global estimates. Dhaka data is a regional model product,
not a local station measurement. Zone queries use the boundary bounding-box centre,
not a surveyed sensor or guaranteed polygon centroid. Nearby zones can share a grid
cell. This feature does not train or depend on the excluded NAG-034 ML story.

The request is fixed to the provider HTTPS endpoint, US AQI, PM2.5 and PM10,
UTC Unix timestamps and the global domain. A live schema check confirmed units
USAQI and μg/m³. Requests time out after five seconds, are cached per zone/point
for two minutes and serialized with a one-second maximum gate wait. A failed
refresh retains the original reading/time labelled Stale for at most six hours.
No reading or unusable data means Unknown, never zero/Good. Values older than
90 minutes are also labelled Stale. Set `AirQuality:Enabled=false` to disable
upstream calls. Provider access is subject to its usage terms and availability.
