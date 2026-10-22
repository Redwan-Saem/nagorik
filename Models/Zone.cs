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

    public ZoneRiskLevel RiskLevel { get; set; } = ZoneRiskLevel.Unknown;

    // Null means no computation has run; creation must not imply a fresh prediction.
    public DateTime? LastComputedAtUtc { get; set; }
}
