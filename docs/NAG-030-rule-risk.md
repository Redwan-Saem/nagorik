# NAG-030: rule-based zone risk

## T-030.1 — zone vulnerability (Swapnil)

`feature/T-030.1-zone-vulnerability` adds nullable elevation (metres), drainage
density (km/km²), and historical waterlogging score (0..1). Existing zones remain
unknown until data is supplied. These are not surveyed values and are not invented
by the weather collector. Database constraints reject out-of-range inputs.

Apply migrations with the app stopped: `dotnet ef database update`.
This task does not automatically migrate the working demo database.
