using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public class ZoneRiskAlertRecipients(AppDbContext db)
{
    public virtual async Task<IReadOnlyList<string>> ListAsync(int zoneId, string code, CancellationToken ct = default)
    {
        var canonical = await db.ResidentZoneSubscriptions.AsNoTracking().Where(s => s.ZoneId == zoneId && s.AlertsEnabled &&
            s.Resident.Role == "Resident" && db.NotificationPreferences.Any(p => p.UserId == s.ResidentId.ToString() && p.NotificationsEnabled))
            .Select(s => s.ResidentId.ToString()).ToListAsync(ct);
        var legacy = await (from subscription in db.AreaSubscriptions
               join preference in db.NotificationPreferences on subscription.UserId equals preference.UserId
               join user in db.Users on subscription.UserId equals user.Id.ToString()
               where subscription.ZoneId == code && preference.NotificationsEnabled && user.Role == "Resident" &&
                   !db.ResidentZoneSubscriptions.Any(s => s.ResidentId == user.Id && s.ZoneId == zoneId)
               select subscription.UserId).Distinct().ToListAsync(ct);
        return canonical.Concat(legacy).Distinct(StringComparer.Ordinal).ToList();
    }
}

public sealed class ZoneRiskAlertService(AppDbContext db, ZoneRiskAlertRecipients recipients,
    INotificationService notifications, TimeProvider clock, ILogger<ZoneRiskAlertService> logger,
    IOptions<RiskAlertOptions>? options = null)
{
    public async Task<int> ProcessAsync(CancellationToken ct = default)
    {
        var settings = options?.Value ?? new RiskAlertOptions();
        if (!settings.Enabled) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var token = Guid.NewGuid().ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO RiskAlertProcessorLeases (Id, Token, LockedUntilUtc) VALUES (1, {""}, {DateTime.MinValue})",ct);
        var claimed = await db.RiskAlertProcessorLeases.Where(l=>l.Id==1 && l.LockedUntilUtc<=now)
            .ExecuteUpdateAsync(u=>u.SetProperty(l=>l.Token,token).SetProperty(l=>l.LockedUntilUtc,now.AddMinutes(settings.LeaseMinutes)),ct);
        if (claimed == 0) return 0;
        db.ChangeTracker.Clear();
        try
        {
            var events = await db.RiskAlertEvents.AsNoTracking().Include(e=>e.RiskHistory).ThenInclude(h=>h.Zone)
                .Where(e=>e.ProcessedAtUtc==null).OrderBy(e=>e.Id).Take(100).ToListAsync(ct);
            var processed = 0;
            foreach (var item in events)
            {
                if (!await RenewAsync(token,settings,ct)) return processed;
                var history = item.RiskHistory;
                var ids = Eligible(history,settings) ? await recipients.ListAsync(history.ZoneId,history.Zone.Code,ct) : [];
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                foreach (var userId in ids)
                {
                    if (!int.TryParse(userId,out var residentId) || residentId<=0) continue;
                    var created = clock.GetUtcNow().UtcDateTime;
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO RiskAlertDeliveries (EventId, ResidentId, ZoneId, State, Attempts, CreatedAtUtc) VALUES ({item.Id}, {residentId}, {history.ZoneId}, {"Pending"}, 0, {created})",ct);
                }
                var completed = clock.GetUtcNow().UtcDateTime;
                await db.RiskAlertEvents.Where(e=>e.Id==item.Id && e.ProcessedAtUtc==null).ExecuteUpdateAsync(u=>u.SetProperty(e=>e.ProcessedAtUtc,completed),ct);
                await transaction.CommitAsync(ct); processed++;
            }
            now = clock.GetUtcNow().UtcDateTime;
            await db.RiskAlertDeliveries.Where(d=>d.State==RiskDeliveryState.Sending && d.LeaseUntilUtc<=now && d.Attempts>=settings.MaxAttempts)
                .ExecuteUpdateAsync(u=>u.SetProperty(d=>d.State,RiskDeliveryState.Failed)
                    .SetProperty(d=>d.LastError,"Attempt limit reached after an expired lease; delivery outcome is unconfirmed.")
                    .SetProperty(d=>d.LeaseUntilUtc,(DateTime?)null).SetProperty(d=>d.NextAttemptAtUtc,(DateTime?)null),ct);
            var deliveries = await db.RiskAlertDeliveries.AsNoTracking().Include(d=>d.Event).ThenInclude(e=>e.RiskHistory).ThenInclude(h=>h.Zone)
                .Where(d=>d.Attempts<settings.MaxAttempts && (d.State==RiskDeliveryState.Pending ||
                    (d.State==RiskDeliveryState.Failed && d.NextAttemptAtUtc<=now) ||
                    (d.State==RiskDeliveryState.Sending && d.LeaseUntilUtc<=now)))
                .OrderBy(d=>d.Id).Take(100).ToListAsync(ct);
            foreach (var delivery in deliveries)
            {
                if (!await RenewAsync(token,settings,ct)) return processed;
                now = clock.GetUtcNow().UtcDateTime;
                var history = delivery.Event.RiskHistory;
                var optedIn = Eligible(history,settings) && (await recipients.ListAsync(history.ZoneId,history.Zone.Code,ct)).Contains(delivery.ResidentId.ToString());
                var cutoff = now.AddMinutes(-settings.CooldownMinutes);
                var coolingDown = settings.CooldownMinutes>0 && await db.RiskAlertDeliveries.AnyAsync(d=>d.ResidentId==delivery.ResidentId &&
                    d.ZoneId==delivery.ZoneId && d.State==RiskDeliveryState.Sent && d.SentAtUtc>cutoff,ct);
                if (!await RenewAsync(token,settings,ct)) return processed;
                now = clock.GetUtcNow().UtcDateTime;
                if (!optedIn || coolingDown)
                {
                    var reason = optedIn ? "Suppressed by per-area notification cooldown." : "Opted out or risk evaluation is no longer current.";
                    await db.RiskAlertDeliveries.Where(d=>d.Id==delivery.Id && d.State==delivery.State && d.Attempts==delivery.Attempts)
                        .ExecuteUpdateAsync(u=>u.SetProperty(d=>d.State,RiskDeliveryState.Suppressed).SetProperty(d=>d.LastError,reason),ct);
                    continue;
                }
                var acquired = await db.RiskAlertDeliveries.Where(d=>d.Id==delivery.Id && d.Attempts==delivery.Attempts &&
                    (d.State==RiskDeliveryState.Pending || d.State==RiskDeliveryState.Failed || (d.State==RiskDeliveryState.Sending && d.LeaseUntilUtc<=now)))
                    .ExecuteUpdateAsync(u=>u.SetProperty(d=>d.State,RiskDeliveryState.Sending).SetProperty(d=>d.Attempts,d=>d.Attempts+1)
                        .SetProperty(d=>d.ClaimToken,token).SetProperty(d=>d.LastAttemptAtUtc,now)
                        .SetProperty(d=>d.LeaseUntilUtc,now.AddMinutes(settings.LeaseMinutes)),ct);
                if(acquired==0)continue;
                var delivered = false; string? failure = null;
                try
                {
                    using var sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    sendTimeout.CancelAfter(TimeSpan.FromSeconds(settings.SendTimeoutSeconds));
                    var message=ZoneRiskAlertMessage.Build(history);
                    delivered = await notifications.SendToUserAsync(delivery.ResidentId.ToString(),message.Title,message.Body,
                        message.Url,history.ComputedAtUtc,sendTimeout.Token).WaitAsync(sendTimeout.Token)>0;
                    if(!delivered)failure="No push endpoint confirmed delivery.";
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
                catch(Exception error){failure=$"Delivery failed ({error.GetType().Name}).";logger.LogWarning("Risk receipt {ReceiptId}: {Failure}",delivery.Id,failure);}
                var finished = clock.GetUtcNow().UtcDateTime;
                await db.RiskAlertDeliveries.Where(d=>d.Id==delivery.Id && d.State==RiskDeliveryState.Sending && d.ClaimToken==token)
                    .ExecuteUpdateAsync(u=>u.SetProperty(d=>d.State,delivered?RiskDeliveryState.Sent:RiskDeliveryState.Failed)
                        .SetProperty(d=>d.SentAtUtc,delivered?(DateTime?)finished:null)
                        .SetProperty(d=>d.NextAttemptAtUtc,delivered?null:(DateTime?)finished.AddMinutes(settings.RetryMinutes))
                        .SetProperty(d=>d.LeaseUntilUtc,(DateTime?)null).SetProperty(d=>d.LastError,failure),ct);
            }
            return processed;
        }
        finally
        {
            // Release only this worker's lease; cancellation must not strand it.
            await db.RiskAlertProcessorLeases.Where(l=>l.Id==1 && l.Token==token).ExecuteUpdateAsync(u=>u
                .SetProperty(l=>l.LockedUntilUtc,DateTime.MinValue).SetProperty(l=>l.Token,""),CancellationToken.None);
            db.ChangeTracker.Clear();
        }
    }

    private bool Eligible(RiskHistory history,RiskAlertOptions settings)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        return history.Level is ZoneRiskLevel.High or ZoneRiskLevel.Severe && history.Zone.RiskLevel==history.Level &&
            history.ComputedAtUtc>=now.AddMinutes(-settings.MaxEventAgeMinutes) && history.ComputedAtUtc<=now.AddMinutes(1) &&
            history.Zone.LastComputedAtUtc>=now.AddMinutes(-15) && history.Zone.LastComputedAtUtc<=now.AddMinutes(1);
    }
    private async Task<bool> RenewAsync(string token,RiskAlertOptions settings,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        return await db.RiskAlertProcessorLeases.Where(l=>l.Id==1 && l.Token==token && l.LockedUntilUtc>now)
            .ExecuteUpdateAsync(u=>u.SetProperty(l=>l.LockedUntilUtc,now.AddMinutes(settings.LeaseMinutes)),ct)==1;
    }
}
