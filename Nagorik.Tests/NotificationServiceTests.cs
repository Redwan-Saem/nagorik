using Microsoft.EntityFrameworkCore;
using Moq;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task SendsToEverySubscriptionOfTheUser()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);
            TestDb.AddUser(db, 2);

            db.UserPushSubscriptions.AddRange(
                new UserPushSubscription
                {
                    UserId = "1",
                    Endpoint = "endpoint-1",
                    P256dh = "p256dh-1",
                    Auth = "auth-1"
                },
                new UserPushSubscription
                {
                    UserId = "1",
                    Endpoint = "endpoint-2",
                    P256dh = "p256dh-2",
                    Auth = "auth-2"
                },
                new UserPushSubscription
                {
                    UserId = "2",
                    Endpoint = "endpoint-3",
                    P256dh = "p256dh-3",
                    Auth = "auth-3"
                });

            await db.SaveChangesAsync();

            var sender = new Mock<IPushSender>();

            sender
                .Setup(x => x.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new PushResult(true, 201));

            var service = new NotificationService(
                db,
                sender.Object);

            var result = await service.SendToUserAsync(
                "1",
                "Test",
                "Hello",
                "/");

            Assert.Equal(2, result);

            sender.Verify(x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Exactly(2));
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task UserWithNoSubscriptions_ReturnsZero_AndSendsNothing()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var sender = new Mock<IPushSender>();

            var service = new NotificationService(
                db,
                sender.Object);

            var result = await service.SendToUserAsync(
                "1",
                "Test",
                "Hello",
                "/");

            Assert.Equal(0, result);

            sender.Verify(x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task Expired410_RemovesSubscription()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            db.UserPushSubscriptions.Add(
                new UserPushSubscription
                {
                    UserId = "1",
                    Endpoint = "expired-endpoint",
                    P256dh = "p256dh",
                    Auth = "auth"
                });

            await db.SaveChangesAsync();

            var sender = new Mock<IPushSender>();

            sender
                .Setup(x => x.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new PushResult(false, 410));

            var service = new NotificationService(
                db,
                sender.Object);

            var result = await service.SendToUserAsync(
                "1",
                "Test",
                "Hello",
                "/");

            Assert.Equal(0, result);

            var subscription =
                await db.UserPushSubscriptions
                    .FirstOrDefaultAsync();

            Assert.Null(subscription);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task ServerError500_KeepsSubscription()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            db.UserPushSubscriptions.Add(
                new UserPushSubscription
                {
                    UserId = "1",
                    Endpoint = "server-error-endpoint",
                    P256dh = "p256dh",
                    Auth = "auth"
                });

            await db.SaveChangesAsync();

            var sender = new Mock<IPushSender>();

            sender
                .Setup(x => x.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new PushResult(false, 500));

            var service = new NotificationService(
                db,
                sender.Object);

            var result = await service.SendToUserAsync(
                "1",
                "Test",
                "Hello",
                "/");

            Assert.Equal(0, result);

            var subscription =
                await db.UserPushSubscriptions
                    .FirstOrDefaultAsync();

            Assert.NotNull(subscription);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task WritesNotificationLog_WithTitleBodyUrl_AndSentAt()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var sender = new Mock<IPushSender>();

            var service = new NotificationService(
                db,
                sender.Object);

            var before = DateTime.UtcNow;

            await service.SendToUserAsync(
                "1",
                "Flood Alert",
                "Water level is rising",
                "/alerts");

            var after = DateTime.UtcNow;

            var log = await db.NotificationLogs
                .FirstOrDefaultAsync();

            Assert.NotNull(log);
            Assert.Equal("1", log!.UserId);
            Assert.Equal("Flood Alert", log.Title);
            Assert.Equal("Water level is rising", log.Body);
            Assert.Equal("/alerts", log.Url);
            Assert.False(log.Success);
            Assert.InRange(
                log.SentAtUtc,
                before,
                after);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task Payload_ContainsTitleBodyUrlJson()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            db.UserPushSubscriptions.Add(
                new UserPushSubscription
                {
                    UserId = "1",
                    Endpoint = "endpoint",
                    P256dh = "p256dh",
                    Auth = "auth"
                });

            await db.SaveChangesAsync();

            var sender = new Mock<IPushSender>();

            string? capturedPayload = null;

            sender
                .Setup(x => x.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Callback<string, string, string, string>(
                    (_, _, _, payload) =>
                    {
                        capturedPayload = payload;
                    })
                .ReturnsAsync(new PushResult(true, 201));

            var service = new NotificationService(
                db,
                sender.Object);

            await service.SendToUserAsync(
                "1",
                "Title",
                "Body",
                "/test");

            Assert.NotNull(capturedPayload);

            using var document =
                System.Text.Json.JsonDocument.Parse(
                    capturedPayload!);

            var root = document.RootElement;

            Assert.Equal(
                "Title",
                root.GetProperty("title").GetString());

            Assert.Equal(
                "Body",
                root.GetProperty("body").GetString());

            Assert.Equal(
                "/test",
                root.GetProperty("url").GetString());
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task OtherUsersSubscriptions_NeverUsed()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);
            TestDb.AddUser(db, 2);

            db.UserPushSubscriptions.AddRange(
                new UserPushSubscription
                {
                    UserId = "1",
                    Endpoint = "user1-endpoint",
                    P256dh = "p256dh-1",
                    Auth = "auth-1"
                },
                new UserPushSubscription
                {
                    UserId = "2",
                    Endpoint = "user2-endpoint",
                    P256dh = "p256dh-2",
                    Auth = "auth-2"
                });

            await db.SaveChangesAsync();

            var sender = new Mock<IPushSender>();

            sender
                .Setup(x => x.SendAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new PushResult(true, 201));

            var service = new NotificationService(
                db,
                sender.Object);

            await service.SendToUserAsync(
                "1",
                "Test",
                "Hello",
                "/");

            sender.Verify(x => x.SendAsync(
                "user1-endpoint",
                "p256dh-1",
                "auth-1",
                It.IsAny<string>()), Times.Once);

            sender.Verify(x => x.SendAsync(
                "user2-endpoint",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }
}