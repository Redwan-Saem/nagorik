using Microsoft.EntityFrameworkCore;
using Moq;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class PushCancellationTests
{
    [Fact]
    public async Task NotificationTimeout_StopsWaitingForUnresponsivePushWithoutSavingAfterCancellation()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        db.UserPushSubscriptions.Add(new UserPushSubscription
        {
            UserId = "1", Endpoint = "endpoint", P256dh = "key", Auth = "auth"
        });
        await db.SaveChangesAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivery = new TaskCompletionSource<PushResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new Mock<IPushSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() => { started.TrySetResult(); return delivery.Task; });
        using var cancellation = new CancellationTokenSource();
        var pending = new NotificationService(db, sender.Object).SendToUserAsync("1", "SOS", "Body", "/sos/respond/1", ct: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        delivery.SetResult(new PushResult(true, 201));
        Assert.Empty(await db.NotificationLogs.ToListAsync());
        Assert.Single(await db.UserPushSubscriptions.ToListAsync());
    }
}
