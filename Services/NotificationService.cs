using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services
{
    public interface INotificationService
    {
        Task<int> SendToUserAsync(
            string userId,
            string title,
            string body,
            string url,
            DateTime? predictionTimestampUtc = null,
            CancellationToken ct = default);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly IPushSender _sender;

        public NotificationService(
            AppDbContext db,
            IPushSender sender)
        {
            _db = db;
            _sender = sender;
        }

        public async Task<int> SendToUserAsync(
            string userId,
            string title,
            string body,
            string url,
            DateTime? predictionTimestampUtc = null,
            CancellationToken ct = default)
        {
            var payload = JsonSerializer.Serialize(new
            {
                title,
                body,
                url
            });

            var subscriptions = await _db.UserPushSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync(ct);

            int delivered = 0;

            foreach (var subscription in subscriptions)
            {
                var result = await _sender.SendAsync(
                    subscription.Endpoint,
                    subscription.P256dh,
                    subscription.Auth,
                    payload);

                if (result.Success)
                {
                    delivered++;
                }
                else if (result.StatusCode is 404 or 410)
                {
                    _db.UserPushSubscriptions.Remove(subscription);
                }
            }

            _db.NotificationLogs.Add(
                new NotificationLog
                {
                    UserId = userId,
                    Title = title,
                    Body = body,
                    Url = url,
                    Success = delivered > 0,
                    PredictionTimestampUtc = predictionTimestampUtc,
                    SentAtUtc = DateTime.UtcNow
                });

            await _db.SaveChangesAsync(ct);

            return delivered;
        }
    }
}