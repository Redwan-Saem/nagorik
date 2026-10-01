using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public class NotificationSettingsService
{
    private readonly AppDbContext _db;

    public NotificationSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Enabled, List<string> ZoneIds)> GetAsync(string userId)
    {
        var preference = await _db.NotificationPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId);

        var zoneIds = await _db.AreaSubscriptions
            .Where(x => x.UserId == userId)
            .Select(x => x.ZoneId)
            .ToListAsync();

        return (
            preference?.NotificationsEnabled ?? false,
            zoneIds
        );
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        string userId,
        bool enabled,
        List<string> zoneIds)
    {
        zoneIds ??= new List<string>();

        var validZoneIds = await _db.ZoneMapData
            .Select(x => x.ZoneId)
            .Distinct()
            .ToListAsync();

        var invalidZoneIds = zoneIds
            .Except(validZoneIds)
            .ToList();

        if (invalidZoneIds.Count > 0)
        {
            return (
                false,
                $"Invalid zone ID(s): {string.Join(", ", invalidZoneIds)}"
            );
        }

        var preference = await _db.NotificationPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (preference == null)
        {
            preference = new NotificationPreference
            {
                UserId = userId,
                NotificationsEnabled = enabled
            };

            _db.NotificationPreferences.Add(preference);
        }
        else
        {
            preference.NotificationsEnabled = enabled;
        }

        var existingSubscriptions = await _db.AreaSubscriptions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _db.AreaSubscriptions.RemoveRange(existingSubscriptions);

        var uniqueZoneIds = zoneIds
            .Distinct()
            .ToList();

        foreach (var zoneId in uniqueZoneIds)
        {
            _db.AreaSubscriptions.Add(new AreaSubscription
            {
                UserId = userId,
                ZoneId = zoneId
            });
        }

        await _db.SaveChangesAsync();

        return (true, null);
    }

    public async Task SubscribePushAsync(
        string userId,
        string endpoint,
        string p256dh,
        string auth)
    {
        var existing = await _db.UserPushSubscriptions
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint);

        if (existing != null)
        {
            existing.UserId = userId;
            existing.P256dh = p256dh;
            existing.Auth = auth;
        }
        else
        {
            _db.UserPushSubscriptions.Add(new UserPushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task UnsubscribePushAsync(
        string userId,
        string endpoint)
    {
        var subscription = await _db.UserPushSubscriptions
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                     x.Endpoint == endpoint);

        if (subscription != null)
        {
            _db.UserPushSubscriptions.Remove(subscription);
            await _db.SaveChangesAsync();
        }
    }
}