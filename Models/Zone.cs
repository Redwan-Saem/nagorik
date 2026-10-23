namespace Nagorik.Api.Models;

public enum ZoneRiskLevel
{
    Unknown,
    Low,
    Medium,
    High,
    Severe
}

// Canonical map zone. Legacy WaterloggingRisk/ZoneMapData remain available
// until their readers and alert subscriptions are migrated explicitly.
public sealed class Zone
{
    public int Id { get; set; }

    // Stable external identifier, compatible with existing Z1-style subscriptions.
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    // A GeoJSON Polygon/MultiPolygon geometry; positions use [longitude, latitude].
    // This is not a Leaflet [latitude, longitude] coordinate array.
    public string BoundaryGeoJson { get; set; } = "";
    public string? BoundarySource { get; set; }
    public bool? BoundaryIsSample { get; set; }
    public string? InputSource { get; set; }
    public bool? InputIsSample { get; set; }
    public int GeoRevision { get; set; }
    public DateTime? GeoUpdatedAtUtc { get; set; }

    public ZoneRiskLevel RiskLevel { get; set; } = ZoneRiskLevel.Unknown;

    // Null means no computation has run; creation must not imply a fresh prediction.
    public DateTime? LastComputedAtUtc { get; set; }

    // Unknown until surveyed/imported. Density is km of drainage per square km;
    // history is a normalized 0..1 waterlogging frequency score.
    public double? ElevationMeters { get; set; }
    public double? DrainageDensityKmPerSqKm { get; set; }
    public double? HistoryScore { get; set; }
    public string? RiskFactorsJson { get; set; }
}
