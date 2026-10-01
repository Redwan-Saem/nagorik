using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class NotificationSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsDefaultWhenNoPreferenceExists()
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
    public async Task GetAsync_ReturnsSavedPreferenceAndZones()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.NotificationPreferences.Add(new NotificationPreference
            {
                UserId = "user-1",
                NotificationsEnabled = true
            });

            db.AreaSubscriptions.AddRange(
                new AreaSubscription
                {
                    UserId = "user-1",
                    ZoneId = "ZONE-1"
                },
                new AreaSubscription
                {
                    UserId = "user-1",
                    ZoneId = "ZONE-2"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.GetAsync("user-1");

            Assert.True(result.Enabled);
            Assert.Equal(
                new[] { "ZONE-1", "ZONE-2" },
                result.ZoneIds);
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
                new ZoneMapData { ZoneId = "ZONE-1" },
                new ZoneMapData { ZoneId = "ZONE-2" });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.UpdateAsync(
                "user-1",
                true,
                new List<string> { "ZONE-1", "ZONE-2" });

            Assert.True(result.Success);
            Assert.Null(result.Error);

            var preference = db.NotificationPreferences
                .Single(x => x.UserId == "user-1");

            Assert.True(preference.NotificationsEnabled);

            var zones = db.AreaSubscriptions
                .Where(x => x.UserId == "user-1")
                .Select(x => x.ZoneId)
                .ToList();

            Assert.Equal(
                new[] { "ZONE-1", "ZONE-2" },
                zones);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidZone()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.ZoneMapData.Add(
                new ZoneMapData { ZoneId = "ZONE-1" });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            var result = await service.UpdateAsync(
                "user-1",
                true,
                new List<string> { "INVALID-ZONE" });

            Assert.False(result.Success);
            Assert.Contains("INVALID-ZONE", result.Error);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateAsync_ReplacesExistingSubscriptions()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            db.ZoneMapData.AddRange(
                new ZoneMapData { ZoneId = "ZONE-1" },
                new ZoneMapData { ZoneId = "ZONE-2" });

            db.AreaSubscriptions.Add(
                new AreaSubscription
                {
                    UserId = "user-1",
                    ZoneId = "ZONE-1"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            await service.UpdateAsync(
                "user-1",
                true,
                new List<string> { "ZONE-2" });

            var zones = db.AreaSubscriptions
                .Where(x => x.UserId == "user-1")
                .Select(x => x.ZoneId)
                .ToList();

            Assert.Equal(new[] { "ZONE-2" }, zones);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribePushAsync_CreatesSubscription()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            var service = new NotificationSettingsService(db);

            await service.SubscribePushAsync(
                "user-1",
                "endpoint-1",
                "p256dh-1",
                "auth-1");

            var subscription = db.UserPushSubscriptions
                .Single();

            Assert.Equal("user-1", subscription.UserId);
            Assert.Equal("endpoint-1", subscription.Endpoint);
            Assert.Equal("p256dh-1", subscription.P256dh);
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
            db.UserPushSubscriptions.Add(
                new UserPushSubscription
                {
                    UserId = "user-1",
                    Endpoint = "endpoint-1",
                    P256dh = "old-key",
                    Auth = "old-auth"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            await service.SubscribePushAsync(
                "user-2",
                "endpoint-1",
                "new-key",
                "new-auth");

            var subscriptions = db.UserPushSubscriptions.ToList();

            Assert.Single(subscriptions);
            Assert.Equal("user-2", subscriptions[0].UserId);
            Assert.Equal("new-key", subscriptions[0].P256dh);
            Assert.Equal("new-auth", subscriptions[0].Auth);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribePushAsync_RemovesOnlyUsersSubscription()
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
                    Endpoint = "endpoint-1",
                    P256dh = "key-2",
                    Auth = "auth-2"
                });

            await db.SaveChangesAsync();

            var service = new NotificationSettingsService(db);

            await service.UnsubscribePushAsync(
                "user-1",
                "endpoint-1");

            var remaining = db.UserPushSubscriptions.ToList();

            Assert.Single(remaining);
            Assert.Equal("user-2", remaining[0].UserId);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }
}