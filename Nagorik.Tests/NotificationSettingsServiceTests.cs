using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class NotificationSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsPreferenceAndZones()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.NotificationPreferences.Add(
                new NotificationPreference
                {
                    UserId = "user-1",
                    NotificationsEnabled = true
                });

            db.AreaSubscriptions.AddRange(
                new AreaSubscription
                {
                    UserId = "user-1",
                    ZoneId = "Z1"
                },
                new AreaSubscription
                {
                    UserId = "user-1",
                    ZoneId = "Z2"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.GetAsync("user-1");

            Assert.True(result.Enabled);
            Assert.Equal(2, result.ZoneIds.Count);
            Assert.Contains("Z1", result.ZoneIds);
            Assert.Contains("Z2", result.ZoneIds);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsFalseAndEmptyZones_WhenNothingExists()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            var service = new NotificationSettingsService(db);

            var result = await service.GetAsync("user-1");

            Assert.False(result.Enabled);
            Assert.Empty(result.ZoneIds);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_CreatesPreferenceAndSubscriptions()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.ZoneMapData.AddRange(
                new ZoneMapData
                {
                    ZoneId = "Z1",
                    RiskLevel = "High",
                    DrainagePumpStatus = "Working",
                    BoundaryGeoJson = "{}"
                },
                new ZoneMapData
                {
                    ZoneId = "Z2",
                    RiskLevel = "Medium",
                    DrainagePumpStatus = "Working",
                    BoundaryGeoJson = "{}"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.UpdateAsync(
                "user-1",
                true,
                new List<string> { "Z1", "Z2" });

            Assert.True(result.Success);
            Assert.Null(result.Error);

            var preference = await db.NotificationPreferences
                .SingleAsync(x => x.UserId == "user-1");

            Assert.True(preference.NotificationsEnabled);

            var zones = await db.AreaSubscriptions
                .Where(x => x.UserId == "user-1")
                .Select(x => x.ZoneId)
                .ToListAsync();

            Assert.Equal(2, zones.Count);
            Assert.Contains("Z1", zones);
            Assert.Contains("Z2", zones);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingPreferenceAndReplacesZones()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.ZoneMapData.AddRange(
                new ZoneMapData
                {
                    ZoneId = "Z1",
                    RiskLevel = "High",
                    DrainagePumpStatus = "Working",
                    BoundaryGeoJson = "{}"
                },
                new ZoneMapData
                {
                    ZoneId = "Z2",
                    RiskLevel = "Medium",
                    DrainagePumpStatus = "Working",
                    BoundaryGeoJson = "{}"
                });

            db.NotificationPreferences.Add(
                new NotificationPreference
                {
                    UserId = "user-1",
                    NotificationsEnabled = true
                });

            db.AreaSubscriptions.Add(
                new AreaSubscription
                {
                    UserId = "user-1",
                    ZoneId = "Z1"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.UpdateAsync(
                "user-1",
                false,
                new List<string> { "Z2" });

            Assert.True(result.Success);
            Assert.Null(result.Error);

            var preference = await db.NotificationPreferences
                .SingleAsync(x => x.UserId == "user-1");

            Assert.False(preference.NotificationsEnabled);

            var zones = await db.AreaSubscriptions
                .Where(x => x.UserId == "user-1")
                .Select(x => x.ZoneId)
                .ToListAsync();

            Assert.Single(zones);
            Assert.Equal("Z2", zones[0]);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_ReturnsErrorForInvalidZone()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.ZoneMapData.Add(
                new ZoneMapData
                {
                    ZoneId = "Z1",
                    RiskLevel = "High",
                    DrainagePumpStatus = "Working",
                    BoundaryGeoJson = "{}"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.UpdateAsync(
                "user-1",
                true,
                new List<string> { "INVALID" });

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Contains("INVALID", result.Error);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_RemovesDuplicateZoneIds()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.ZoneMapData.Add(
                new ZoneMapData
                {
                    ZoneId = "Z1",
                    RiskLevel = "High",
                    DrainagePumpStatus = "Working",
                    BoundaryGeoJson = "{}"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.UpdateAsync(
                "user-1",
                true,
                new List<string> { "Z1", "Z1", "Z1" });

            Assert.True(result.Success);

            var count = await db.AreaSubscriptions
                .CountAsync(x => x.UserId == "user-1");

            Assert.Equal(1, count);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribePushAsync_AddsSubscription()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            var service = new NotificationSettingsService(db);

            await service.SubscribePushAsync(
                "user-1",
                "endpoint-1",
                "key-1",
                "auth-1");

            var subscription = await db.UserPushSubscriptions
                .SingleAsync();

            Assert.Equal("user-1", subscription.UserId);
            Assert.Equal("endpoint-1", subscription.Endpoint);
            Assert.Equal("key-1", subscription.P256dh);
            Assert.Equal("auth-1", subscription.Auth);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribePushAsync_UpdatesExistingEndpoint()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            var service = new NotificationSettingsService(db);

            await service.SubscribePushAsync(
                "user-1",
                "endpoint-1",
                "key-1",
                "auth-1");

            await service.SubscribePushAsync(
                "user-2",
                "endpoint-1",
                "key-2",
                "auth-2");

            var subscription = await db.UserPushSubscriptions
                .SingleAsync();

            Assert.Equal("user-2", subscription.UserId);
            Assert.Equal("key-2", subscription.P256dh);
            Assert.Equal("auth-2", subscription.Auth);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribePushAsync_RemovesSubscription()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.UserPushSubscriptions.Add(
                new UserPushSubscription
                {
                    UserId = "user-1",
                    Endpoint = "endpoint-1",
                    P256dh = "key-1",
                    Auth = "auth-1"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            await service.UnsubscribePushAsync(
                "user-1",
                "endpoint-1");

            var count = await db.UserPushSubscriptions.CountAsync();

            Assert.Equal(0, count);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribePushAsync_DoesNotRemoveAnotherUsersSubscription()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.UserPushSubscriptions.AddRange(
                new UserPushSubscription
                {
                    UserId = "user-1",
                    Endpoint = "endpoint-1",
                    P256dh = "key-1",
                    Auth = "auth-1"
                },
                new UserPushSubscription
                {
                    UserId = "user-2",
                    Endpoint = "endpoint-2",
                    P256dh = "key-2",
                    Auth = "auth-2"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            await service.UnsubscribePushAsync(
                "user-1",
                "endpoint-1");

            var remaining = await db.UserPushSubscriptions
                .ToListAsync();

            Assert.Single(remaining);
            Assert.Equal("user-2", remaining[0].UserId);
            Assert.Equal("endpoint-2", remaining[0].Endpoint);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }
}