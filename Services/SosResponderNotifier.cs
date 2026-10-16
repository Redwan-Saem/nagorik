using System.Globalization;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public class SosResponderNotifier(SosResponderDirectory directory, IServiceScopeFactory scopes,
    IOptions<SosOptions> options, ILogger<SosResponderNotifier> log)
{
    // Called only after the alert and Triggered event are committed.
    public async Task NotifyCreatedAsync(SosAlert alert)
    {
        try
        {
            var recipients = await directory.GetUserIdsAsync();
            if (recipients.Count == 0)
            {
                log.LogError("SOS {AlertId} is saved but no configured responder accounts were found; no responder was notified", alert.Id);
                return;
            }
            // Independent scopes keep parallel recipient attempts from sharing an EF context.
            await Task.WhenAll(recipients.Select(id => NotifyRecipientAsync(alert, id)));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "SOS responder selection failed for saved alert {AlertId}", alert.Id);
        }
    }

    private async Task NotifyRecipientAsync(SosAlert alert, int userId)
    {
        var latency = (DateTime.UtcNow - alert.CreatedAtUtc).TotalMilliseconds;
        log.LogInformation("SOS {AlertId} responder {UserId} dispatch started after {LatencyMs} ms", alert.Id, userId, latency);
        if (latency >= 60_000)
            log.LogError("SOS {AlertId} responder {UserId} dispatch exceeded the 60 second target", alert.Id, userId);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.NotificationTimeoutSeconds));
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var location = string.IsNullOrWhiteSpace(alert.AddressText)
                ? FormattableString.Invariant($"GPS {alert.Latitude:F5}, {alert.Longitude:F5}")
                : alert.AddressText;
            if (location.Length > 160) location = location[..157] + "…";
            var delivered = await notifications.SendToUserAsync(userId.ToString(CultureInfo.InvariantCulture),
                "SOS assistance requested", $"SOS-{alert.Id:D6}: {location}", $"/sos/respond/{alert.Id}",
                ct: timeout.Token);
            if (delivered == 0)
                log.LogError("SOS {AlertId} responder {UserId} received no push delivery; check subscriptions and notification logs", alert.Id, userId);
            else
                log.LogInformation("SOS {AlertId} delivered to {Delivered} device(s) for responder {UserId}", alert.Id, delivered, userId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "SOS {AlertId} notification attempt failed for responder {UserId}; alert remains saved", alert.Id, userId);
        }
    }
}
