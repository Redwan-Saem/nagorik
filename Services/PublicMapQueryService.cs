using Microsoft.EntityFrameworkCore;

namespace Nagorik.Api.Services;
public sealed record PublicReportProperties(string Reference, string Category, string Status, DateTime CreatedAtUtc);
public sealed record PublicBlockageProperties(int ZoneId, string Status);
public sealed class PublicMapQueryService(AppDbContext db)
{
    public async Task<MapPointCollection<PublicReportProperties>> ReportsAsync(CancellationToken ct = default)
    {
        var verified = db.Reports.AsNoTracking().Where(r => r.IsPubliclyVerified);
        var total = await verified.CountAsync(ct);
        var rows = await verified.Where(r => r.Latitude >= -90 && r.Latitude <= 90 && r.Longitude >= -180 && r.Longitude <= 180 &&
            (r.Latitude != 0 || r.Longitude != 0)).OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(5000)
            .Select(r => new { r.Id, r.Category, r.Status, r.CreatedAt, r.Latitude, r.Longitude }).ToListAsync(ct);
        return new("FeatureCollection", rows.Select(r => new MapPointFeature<PublicReportProperties>("Feature", r.Id,
            new("Point", [r.Longitude, r.Latitude]), new($"R-{r.Id:D6}", r.Category.ToString(), r.Status.ToString(),
                DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)))).ToList(), Math.Max(0, total - rows.Count));
    }
    public async Task<MapPointCollection<PublicBlockageProperties>> BlockagesAsync(CancellationToken ct = default)
    {
        var query = db.DrainagePoints.AsNoTracking().Where(p => p.Status == Nagorik.Api.Models.DrainageStatus.Blocked);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(p => p.Id).Take(5000).Select(p => new { p.Id, p.ZoneId, p.Latitude, p.Longitude }).ToListAsync(ct);
        return new("FeatureCollection", rows.Select(p => new MapPointFeature<PublicBlockageProperties>("Feature", p.Id,
            new("Point", [p.Longitude, p.Latitude]), new(p.ZoneId, "Blocked"))).ToList(), Math.Max(0, total - rows.Count));
    }
}
