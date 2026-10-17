namespace Nagorik.Api.Models;

public enum AnomalyType
{
    ExposedWire,
    GasLeak,
    FallenPole,
    PowerOutage,
    WaterLeak,
    Other
}

// Assigned by routing rules, not by the resident. Unassigned is an explicit no-match result.
public enum UtilityProvider
{
    Unassigned,
    Desco,
    Dpdc,
    WaterAuthority,
    GasAuthority
}
