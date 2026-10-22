using Microsoft.EntityFrameworkCore;

namespace Nagorik.Api.Services;

public class ZoneRiskAlertRecipients(AppDbContext db)
{
    public virtual async Task<IReadOnlyList<string>> ListAsync(int zoneId, string code, CancellationToken ct = default) =>
        await (from subscription in db.AreaSubscriptions
               join preference in db.NotificationPreferences on subscription.UserId equals preference.UserId
               join user in db.Users on subscription.UserId equals user.Id.ToString()
               where subscription.ZoneId == code && preference.NotificationsEnabled && user.Role == "Resident"
               select subscription.UserId).Distinct().ToListAsync(ct);
}

public sealed class ZoneRiskAlertService(AppDbContext db, ZoneRiskAlertRecipients recipients,
    INotificationService notifications, TimeProvider clock, ILogger<ZoneRiskAlertService> logger)
{
    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        var events = await db.RiskAlertEvents.Include(e => e.RiskHistory).ThenInclude(h => h.Zone)
            .Where(e => e.ProcessedAtUtc == null).OrderBy(e => e.Id).Take(100).ToListAsync(ct);
        var count = 0;
        foreach (var item in events)
        {
            try
            {
                var history = item.RiskHistory;
                foreach (var userId in await recipients.ListAsync(history.ZoneId, history.Zone.Code, ct))
                    await notifications.SendToUserAsync(userId, $"Zone risk alert: {history.Zone.Name}",
                        $"Rule-based assessment changed to {history.Level}. Evaluated {history.ComputedAtUtc:u}. Check the map for details.",
                        $"/map?zone={history.ZoneId}", history.ComputedAtUtc, ct);
                item.ProcessedAtUtc = clock.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct); count++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error) { logger.LogWarning(error, "Risk alert event {EventId} failed; retained for retry", item.Id); }
        }
        return count;
    }
}
