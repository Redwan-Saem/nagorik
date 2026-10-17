namespace Nagorik.Api.Models;

public sealed record UtilityAnomalyFormViewModel(IReadOnlyList<ReportArea> Areas)
{
    public static string TypeLabel(AnomalyType type) => type switch
    {
        AnomalyType.ExposedWire => "Exposed wire",
        AnomalyType.GasLeak => "Gas leak",
        AnomalyType.FallenPole => "Fallen pole",
        AnomalyType.PowerOutage => "Power outage",
        AnomalyType.WaterLeak => "Water leak",
        _ => "Other"
    };
}
