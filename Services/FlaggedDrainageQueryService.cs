using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;
public sealed record FlaggedDrainagePoint(int Id, double Latitude, double Longitude, string Status, int ZoneId,
    string ZoneCode, string ZoneName, string RiskLevel, DateTime LastComputedAtUtc);
public sealed class FlaggedDrainageQueryService(AppDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<FlaggedDrainagePoint>> ListAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime; var cutoff = now.AddMinutes(-15);
        return await db.DrainagePoints.AsNoTracking().Where(p =>
            (p.Zone.RiskLevel == ZoneRiskLevel.High || p.Zone.RiskLevel == ZoneRiskLevel.Severe) &&
            p.Zone.LastComputedAtUtc >= cutoff && p.Zone.LastComputedAtUtc <= now.AddMinutes(1))
            .OrderByDescending(p => p.Zone.RiskLevel == ZoneRiskLevel.Severe).ThenBy(p => p.Id).Take(5000)
            .Select(p => new FlaggedDrainagePoint(p.Id, p.Latitude, p.Longitude, p.Status.ToString(), p.ZoneId,
                p.Zone.Code, p.Zone.Name, p.Zone.RiskLevel.ToString(), p.Zone.LastComputedAtUtc!.Value)).ToListAsync(ct);
    }
}
