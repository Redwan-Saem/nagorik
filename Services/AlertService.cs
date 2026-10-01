using Microsoft.EntityFrameworkCore;

namespace Nagorik.Api.Services;

public class AlertService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    private readonly IConfiguration _cfg;

    public AlertService(
        AppDbContext db,
        INotificationService notify,
        IConfiguration cfg)
    {
        _db = db;
        _notify = notify;
        _cfg = cfg;
    }

    public async Task<int> ProcessPendingAsync(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;

        var minimumRisk =
            _cfg["Alerts:MinimumRiskLevel"] ?? "High";

        var risks = await _db.WaterloggingRisks
            .Where(r => r.AlertProcessedAtUtc == null)
            .ToListAsync();

        var pending = risks
            .Where(r => RiskRank(r.RiskLevel) >= RiskRank(minimumRisk))
            .ToList();

        int sent = 0;

        foreach (var risk in pending)
        {
            var userIds = await (
                from a in _db.AreaSubscriptions
                join p in _db.NotificationPreferences
                    on a.UserId equals p.UserId
                where a.ZoneId == risk.ZoneId
                      && p.NotificationsEnabled
                select a.UserId
            )
            .Distinct()
            .ToListAsync();

            var title =
                $"Waterlogging alert: {risk.ZoneName}";

            var from = risk.ExpectedStartUtc.HasValue
                ? DhakaTime.ToLocal(risk.ExpectedStartUtc.Value)
                : (DateTime?)null;

            var to = risk.ExpectedEndUtc.HasValue
                ? DhakaTime.ToLocal(risk.ExpectedEndUtc.Value)
                : (DateTime?)null;

            var window =
                from.HasValue && to.HasValue
                    ? $"{from:h:mm tt} to {to:h:mm tt}, {from:d MMM}"
                    : "time to be confirmed";

            var body =
                $"{risk.RiskLevel} severity expected {window}.";

            foreach (var uid in userIds)
            {
                await _notify.SendToUserAsync(
                    uid,
                    title,
                    body,
                    $"/Predictions/Details/{risk.Id}",
                    risk.LastUpdated
                );

                sent++;
            }

            risk.AlertProcessedAtUtc = now;
        }

        await _db.SaveChangesAsync();

        return sent;
    }

    private static int RiskRank(string? level) =>
        level?.Trim().ToLowerInvariant() switch
        {
            "low" => 1,
            "medium" => 2,
            "high" => 3,
            "critical" => 4,
            _ => 0
        };
}