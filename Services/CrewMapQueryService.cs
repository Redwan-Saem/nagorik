using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;
public sealed record CrewMapProperties(string Name, bool IsBusy, DateTime LastSeenAtUtc, double AgeMinutes, bool IsStale);
public sealed class CrewMapQueryService(AppDbContext db, TimeProvider clock)
{
    public async Task<MapPointCollection<CrewMapProperties>> ListAsync(CancellationToken ct = default)
    {
        var total = await db.Crews.CountAsync(ct);
        var crews = await db.Crews.AsNoTracking().Where(c => c.LastLatitude != null && c.LastLongitude != null && c.LastSeenAtUtc != null)
            .OrderBy(c => c.Id).Take(MapFeatureQueryService.FeatureLimit).Select(c => new { c.Id, c.Name, c.LastLatitude,
                c.LastLongitude, c.LastSeenAtUtc, Busy = db.Reports.Any(r => r.AssignedCrewId == c.Id &&
                    (r.Status == ReportStatus.Assigned || r.Status == ReportStatus.InProgress)) }).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var features = crews.Select(c => {
            var timestamp = DateTime.SpecifyKind(c.LastSeenAtUtc!.Value, DateTimeKind.Utc);
            var age = Math.Max(0, (now - timestamp).TotalMinutes);
            return new MapPointFeature<CrewMapProperties>("Feature", c.Id, new("Point", [c.LastLongitude!.Value, c.LastLatitude!.Value]),
                new(c.Name, c.Busy, timestamp, Math.Round(age, 1), age > 15 || timestamp > now.AddMinutes(1)));
        }).ToList();
        return new("FeatureCollection", features, Math.Max(0, total - features.Count));
    }
}
