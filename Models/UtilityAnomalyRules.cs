namespace Nagorik.Api.Models;

public static class UtilityAnomalyRules
{
    public const int MaxPhotoCount = 3;
    public const int MaxReferenceLength = 32;

    public static bool IsHighRisk(AnomalyType type) => type switch
    {
        AnomalyType.ExposedWire or AnomalyType.GasLeak or AnomalyType.FallenPole => true,
        AnomalyType.PowerOutage or AnomalyType.WaterLeak or AnomalyType.Other => false,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown utility anomaly type.")
    };

    public static ReportUrgency GetUrgency(AnomalyType type) =>
        IsHighRisk(type) ? ReportUrgency.High : ReportUrgency.Normal;
}
