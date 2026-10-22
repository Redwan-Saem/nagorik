# NAG-030: rule-based zone risk

## T-030.1 — zone vulnerability (Swapnil)

`feature/T-030.1-zone-vulnerability` adds nullable elevation (metres), drainage
density (km/km²), and historical waterlogging score (0..1). Existing zones remain
unknown until data is supplied. These are not surveyed values and are not invented
by the weather collector. Database constraints reject out-of-range inputs.

Apply migrations with the app stopped: `dotnet ef database update`.
This task does not automatically migrate the working demo database.

## T-030.2 — rule baseline (Labiba)

`RiskPredictionService` compares hourly equivalents of both past and forecast
rainfall: maximum of 1h, 3h/3 and 6h/6. Vulnerability is
`1 + clamp((10-elevation)/10,0,1) + clamp((5-density)/5,0,1) + history`.
Score = intensity × vulnerability. Defaults are Medium ≥5, High ≥15, Severe ≥30;
lower scores are Low. Configure the `RiskScoring` section (or environment keys
`RiskScoring__MediumThreshold`, `RiskScoring__HighThreshold`,
`RiskScoring__SevereThreshold`). Thresholds must increase strictly.
Missing vulnerability, stale/missing weather, or unaligned windows produce Unknown.
These coefficients are transparent demonstration rules, not scientifically
calibrated waterlogging probabilities. Open-Meteo rainfall is model data.

## T-030.3 — scheduled computation (Redwan)

At startup and every five minutes `RiskBackgroundService` evaluates all zones,
publishing their level, evaluation timestamp and exact input/threshold snapshots
in one transaction. Missing or stale inputs yield Unknown with a reason, not a
fresh Low value. Failures are logged and retried next cycle. Configure
`RiskJob__Enabled=false` to disable or `RiskJob__PollMinutes` (1..1440).
Weather and risk jobs are independent: new weather appears on the next risk cycle.
The old `WaterloggingRisks` and `ZoneMapData` stores are not updated; this story
uses canonical `Zones`. Legacy resident alerts are a later story.
