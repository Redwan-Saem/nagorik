# NAG-018 — transport routes and fares

Scope: directed stored transport graph, configurable BDT fares, multiple loop-free
options, fastest/cheapest/fewest-transfer ordering, safe detail, local landmark
suggestions, optional GPS and map pins. Coordinates must be within the explicitly
bounded Dhaka service rectangle (23.60–23.95 N, 90.20–90.58 E). Equal points fail.
No network or missing data may produce invented routes or fares.

| Task | Owner | Acceptance |
|---|---|---|
| T-018.1 | Redwan | Relational stops, directed links and configurable fare rules; reversible migration; Development-only labelled samples. |
| T-018.2 | Swapnil | Bounded graph search with multiple options where available, area/input validation, transfers/time/distance and no-route state. |
| T-018.3 | Redwan | Per-mode BDT fares, contiguous boarding legs, estimated ranges, explicit missing-rule handling. |
| T-018.4 | Swapnil | Deterministic sorting and detail endpoint exposing leg directions and geometry. |
| T-018.5 | Labiba | Local catalog suggestions, click-only GPS, map pins and schematic route drawing. |
| T-018.6 | Labiba | Accessible origin/destination form, no-search validation, retained inputs. |
| T-018.7 | Rajiya | Responsive options, sort control and selected detail with per-leg fare and map. |
| T-018.8 | Labiba | Bounded timeout/retry, honest empty/error/sample states and meaningful API/client/browser checks. |

Sample routes and fare coefficients are demonstration fixtures, not official
operator service or tariff claims. Stored link distances/times are estimates.
Map lines connect catalog stops schematically, not turn-by-turn road navigation.
Only enabled catalog landmarks have typed suggestions; arbitrary street geocoding
is not supplied by the local provider. GPS and map pins cover other coordinates
where a supported stop is within walking range. Production is empty until curated
data is imported. No dependency on ML.
