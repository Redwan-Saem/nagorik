using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Nagorik.Api.Services;

public sealed record MapPointGeometry(string Type, double[] Coordinates);
public sealed record MapPointFeature<T>(string Type, int Id, MapPointGeometry Geometry, T Properties);
public sealed record MapPointCollection<T>(string Type, IReadOnlyList<MapPointFeature<T>> Features, int OmittedCount);
public sealed record DrainageMapProperties(int ZoneId, string ZoneCode, string ZoneName, string Status);
public sealed record ReportMapProperties(string Reference, string Title, string Location, string Category, string Status, string? PhotoUrl, DateTime CreatedAtUtc);

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

    public async Task<MapPointCollection<ReportMapProperties>> ReportsAsync(CancellationToken ct = default)
    {
        var total = await db.Reports.CountAsync(ct);
        // Legacy address-only utility reports use 0/0; do not place those in the ocean.
        var points = await db.Reports.AsNoTracking().Where(r => r.Latitude >= -90 && r.Latitude <= 90 &&
            r.Longitude >= -180 && r.Longitude <= 180 && (r.Latitude != 0 || r.Longitude != 0))
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(FeatureLimit)
            .Select(r => new { r.Id, r.ReferenceNo, r.Title, r.Location, r.Category, r.Status, r.ImageUrl,
                r.Latitude, r.Longitude, r.CreatedAt }).ToListAsync(ct);
        var features = points.Select(p => new MapPointFeature<ReportMapProperties>("Feature", p.Id,
            new("Point", [p.Longitude, p.Latitude]), new(p.ReferenceNo ?? $"R-{p.Id:D6}", p.Title,
                p.Location, p.Category.ToString(), p.Status.ToString(), SafePhotoUrl(p.ImageUrl), DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc)))).ToList();
        return new("FeatureCollection", features, Math.Max(0, total - features.Count));
    }

    private static string? SafePhotoUrl(string? url) => url != null && (LocalPhotoStorage.IsPrivateUrl(url) ||
        Regex.IsMatch(url, @"\A/uploads/reports/[0-9a-f]{32}\.(?:jpg|jpeg|png)\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) ? url : null;
}
