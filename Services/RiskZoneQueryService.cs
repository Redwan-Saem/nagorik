using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Nagorik.Api.Services;

public sealed record RiskZoneProperties(string Code, string Name, string RiskLevel, DateTime? LastComputedAtUtc,
    RiskComputationDetails? Factors = null,string? BoundarySource=null,bool? BoundaryIsSample=null,string? InputSource=null,bool? InputIsSample=null,int GeoRevision=0);
public sealed record RiskZoneFeature(string Type, int Id, JsonElement Geometry, RiskZoneProperties Properties);
public sealed record RiskZoneCollection(string Type, IReadOnlyList<RiskZoneFeature> Features, int InvalidZoneCount);

public sealed class RiskZoneQueryService(AppDbContext db, ILogger<RiskZoneQueryService> logger)
{
    public async Task<RiskZoneCollection> ListAsync(CancellationToken ct = default)
    {
        var zones = await db.Zones.AsNoTracking().OrderBy(z => z.Code).Select(z => new
            { z.Id, z.Code, z.Name, z.BoundaryGeoJson, z.RiskLevel, z.LastComputedAtUtc, z.RiskFactorsJson,z.BoundarySource,z.BoundaryIsSample,z.InputSource,z.InputIsSample,z.GeoRevision }).ToListAsync(ct);
        var features = new List<RiskZoneFeature>();
        var invalid = 0;
        foreach (var zone in zones)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var document = JsonDocument.Parse(zone.BoundaryGeoJson);
                if (!ValidGeometry(document.RootElement)) throw new JsonException("Invalid zone geometry.");
                features.Add(new("Feature", zone.Id, document.RootElement.Clone(), new(zone.Code, zone.Name,
                    zone.RiskLevel.ToString(), zone.LastComputedAtUtc is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null,
                    ReadFactors(zone.RiskFactorsJson),zone.BoundarySource,zone.BoundaryIsSample,zone.InputSource,zone.InputIsSample,zone.GeoRevision)));
            }
            catch (JsonException)
            {
                invalid++;
                logger.LogWarning("Skipping invalid geometry for map zone {ZoneId}", zone.Id);
            }
        }
        return new("FeatureCollection", features, invalid);
    }

    private static RiskComputationDetails? ReadFactors(string? value)
    {
        if (value == null) return null;
        try { return JsonSerializer.Deserialize<RiskComputationDetails>(value); }
        catch (JsonException) { return null; }
    }

    // Standard GeoJSON uses longitude, latitude. Reject malformed legacy arrays
    // instead of turning corrupt stored data into a broken entire map response.
    private static bool ValidGeometry(JsonElement geometry)
    {
        if (geometry.ValueKind != JsonValueKind.Object || !geometry.TryGetProperty("type", out var type) ||
            !geometry.TryGetProperty("coordinates", out var coordinates) || coordinates.ValueKind != JsonValueKind.Array) return false;
        return type.ValueKind == JsonValueKind.String && type.GetString() switch
        {
            "Polygon" => Polygon(coordinates),
            "MultiPolygon" => coordinates.GetArrayLength() > 0 && coordinates.EnumerateArray().All(Polygon),
            _ => false
        };
    }

    private static bool Polygon(JsonElement polygon) => polygon.ValueKind == JsonValueKind.Array &&
        polygon.GetArrayLength() > 0 && polygon.EnumerateArray().All(ring =>
            ring.ValueKind == JsonValueKind.Array && ring.GetArrayLength() >= 4 &&
            ring.EnumerateArray().All(Position) &&
            ring[0][0].GetDouble() == ring[ring.GetArrayLength() - 1][0].GetDouble() &&
            ring[0][1].GetDouble() == ring[ring.GetArrayLength() - 1][1].GetDouble());

    private static bool Position(JsonElement position) => position.ValueKind == JsonValueKind.Array &&
        position.GetArrayLength() == 2 && position[0].ValueKind == JsonValueKind.Number && position[1].ValueKind == JsonValueKind.Number &&
        position[0].TryGetDouble(out var lon) && position[1].TryGetDouble(out var lat) &&
        double.IsFinite(lon) && double.IsFinite(lat) && lon is >= -180 and <= 180 && lat is >= -90 and <= 90;
}
