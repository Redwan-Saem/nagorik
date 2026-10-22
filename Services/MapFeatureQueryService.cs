using Microsoft.EntityFrameworkCore;

namespace Nagorik.Api.Services;

public sealed record MapPointGeometry(string Type, double[] Coordinates);
public sealed record MapPointFeature<T>(string Type, int Id, MapPointGeometry Geometry, T Properties);
public sealed record MapPointCollection<T>(string Type, IReadOnlyList<MapPointFeature<T>> Features, int OmittedCount);
public sealed record DrainageMapProperties(int ZoneId, string ZoneCode, string ZoneName, string Status);

public sealed class MapFeatureQueryService(AppDbContext db)
{
    // Explicit cap keeps a corrupted/imported dataset from producing an unbounded response.
    // OmittedCount is surfaced to the map user; results are never silently truncated.
    public const int FeatureLimit = 5000;

    public async Task<MapPointCollection<DrainageMapProperties>> DrainageAsync(CancellationToken ct = default)
    {
        var total = await db.DrainagePoints.CountAsync(ct);
        var points = await db.DrainagePoints.AsNoTracking().OrderBy(p => p.Id).Take(FeatureLimit)
            .Select(p => new { p.Id, p.Latitude, p.Longitude, p.ZoneId, p.Zone.Code, p.Zone.Name, p.Status }).ToListAsync(ct);
        var features = points.Select(p => new MapPointFeature<DrainageMapProperties>("Feature", p.Id,
            new("Point", [p.Longitude, p.Latitude]), new(p.ZoneId, p.Code, p.Name, p.Status.ToString()))).ToList();
        return new("FeatureCollection", features, Math.Max(0, total - features.Count));
    }
}
