using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public enum UtilityRoutingOutcome { Matched, NoMatch, Ambiguous, InvalidInput }

public sealed record UtilityRoutingResult(UtilityRoutingOutcome Outcome, UtilityProvider Utility,
    int? RuleId, IReadOnlyList<int> MatchingRuleIds, string Message);

public class UtilityRoutingService(AppDbContext db)
{
    public async Task<UtilityRoutingResult> ResolveAsync(AnomalyType type, int? areaId = null,
        double? latitude = null, double? longitude = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(type) || areaId <= 0 || latitude.HasValue != longitude.HasValue ||
            latitude.HasValue && (!double.IsFinite(latitude.Value) || latitude < -90 || latitude > 90) ||
            longitude.HasValue && (!double.IsFinite(longitude.Value) || longitude < -180 || longitude > 180))
            return Fallback(UtilityRoutingOutcome.InvalidInput, "Provide a valid anomaly type, area, and coordinate pair.");
        if (areaId.HasValue && !await db.ReportAreas.AsNoTracking().AnyAsync(a => a.Id == areaId, ct))
            return Fallback(UtilityRoutingOutcome.InvalidInput, "The report area does not exist.");

        var candidates = await db.UtilityRoutingRules.AsNoTracking()
            .Where(r => r.IsActive && (r.AnomalyType == null || r.AnomalyType == type) &&
                (r.AreaId == null || r.AreaId == areaId) &&
                (r.MinLatitude == null || latitude.HasValue && longitude.HasValue &&
                    latitude >= r.MinLatitude && latitude <= r.MaxLatitude &&
                    longitude >= r.MinLongitude && longitude <= r.MaxLongitude))
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.AreaId != null)
            .ThenByDescending(r => r.MinLatitude != null)
            .ThenByDescending(r => r.AnomalyType != null)
            .ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.Utility, r.Priority, HasArea = r.AreaId != null,
                HasBounds = r.MinLatitude != null, HasType = r.AnomalyType != null })
            .ToListAsync(ct);
        if (candidates.Count == 0)
            return Fallback(UtilityRoutingOutcome.NoMatch, "No active utility routing rule matched; leave the provider unassigned for review.");
        var best = candidates[0];
        var tied = candidates.Where(r => r.Priority == best.Priority && r.HasArea == best.HasArea &&
            r.HasBounds == best.HasBounds && r.HasType == best.HasType).ToArray();
        var ids = tied.Select(r => r.Id).ToArray();
        if (tied.Select(r => r.Utility).Distinct().Skip(1).Any())
            return new(UtilityRoutingOutcome.Ambiguous, UtilityProvider.Unassigned, null, ids,
                "Equally ranked routing rules select different providers; leave the provider unassigned for review.");
        return new(UtilityRoutingOutcome.Matched, best.Utility, best.Id, ids, "Matched an active utility routing rule.");
    }

    private static UtilityRoutingResult Fallback(UtilityRoutingOutcome outcome, string message) =>
        new(outcome, UtilityProvider.Unassigned, null, Array.Empty<int>(), message);
}
