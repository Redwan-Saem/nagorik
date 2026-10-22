using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;
public class RiskAlertDeliveryTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTime Now = new(2026,10,23,12,0,0,DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow()=>new(Now);
    }
    private static async Task SetupAsync(AppDbContext db)
    {
        db.Users.Add(new(){Id=1,Role="Resident"});var zone=RiskPredictionTests.Zone();db.Zones.Add(zone);await db.SaveChangesAsync();
        db.NotificationPreferences.Add(new(){UserId="1",NotificationsEnabled=true});
        db.ResidentZoneSubscriptions.Add(new(){ResidentId=1,ZoneId=zone.Id});await db.SaveChangesAsync();
    }
    private static async Task<RiskAlertEvent> EnqueueAsync(AppDbContext db,Clock clock)
    {
        var zone=await db.Zones.SingleAsync();zone.RiskLevel=ZoneRiskLevel.High;zone.LastComputedAtUtc=clock.Now;
        var item=new RiskAlertEvent{CreatedAtUtc=clock.Now,RiskHistory=new(){Zone=zone,Level=ZoneRiskLevel.High,
            PreviousLevel=ZoneRiskLevel.Medium,ComputedAtUtc=clock.Now,FactorsJson="{}"}};
        db.RiskAlertEvents.Add(item);await db.SaveChangesAsync();return item;
    }
    private static Mock<INotificationService> Sender(int result)
    {
        var mock=new Mock<INotificationService>();mock.Setup(n=>n.SendToUserAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),
            It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<CancellationToken>())).ReturnsAsync(result);return mock;
    }
    private static ZoneRiskAlertService Processor(AppDbContext db,Clock clock,INotificationService sender,RiskAlertOptions? options=null)=>
        new(db,new(db),sender,clock,NullLogger<ZoneRiskAlertService>.Instance,Options.Create(options??new()));

    [Fact]
    public async Task SuccessfulReceiptPreventsRepeatsAndCooldownSuppressesRapidNewTransitions()
    {
        var(db,connection)=TestDb.Create();await using var d=db;await using var c=connection;var clock=new Clock();
        await SetupAsync(db);await EnqueueAsync(db,clock);var sender=Sender(1);var worker=Processor(db,clock,sender.Object);
        await worker.ProcessAsync();await worker.ProcessAsync();Assert.Equal(RiskDeliveryState.Sent,(await db.RiskAlertDeliveries.SingleAsync()).State);
        clock.Now=clock.Now.AddMinutes(10);await EnqueueAsync(db,clock);await worker.ProcessAsync();
        Assert.Equal(1,await db.RiskAlertDeliveries.CountAsync(r=>r.State==RiskDeliveryState.Suppressed));
        clock.Now=clock.Now.AddMinutes(61);await EnqueueAsync(db,clock);await worker.ProcessAsync();
        Assert.Equal(2,await db.RiskAlertDeliveries.CountAsync(r=>r.State==RiskDeliveryState.Sent));
        sender.Verify(n=>n.SendToUserAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<CancellationToken>()),Times.Exactly(2));
    }
    [Fact]
    public async Task FailedPushRetriesOnScheduleAndStopsAtAttemptLimit()
    {
        var(db,connection)=TestDb.Create();await using var d=db;await using var c=connection;var clock=new Clock();
        await SetupAsync(db);await EnqueueAsync(db,clock);var sender=Sender(0);var worker=Processor(db,clock,sender.Object,new(){MaxAttempts=2,RetryMinutes=1});
        await worker.ProcessAsync();await worker.ProcessAsync();Assert.Equal(1,(await db.RiskAlertDeliveries.AsNoTracking().SingleAsync()).Attempts);
        clock.Now=clock.Now.AddMinutes(1);await worker.ProcessAsync();clock.Now=clock.Now.AddMinutes(2);await worker.ProcessAsync();
        var receipt=await db.RiskAlertDeliveries.SingleAsync();Assert.Equal(2,receipt.Attempts);Assert.Equal(RiskDeliveryState.Failed,receipt.State);Assert.Null(receipt.SentAtUtc);
        sender.Verify(n=>n.SendToUserAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<CancellationToken>()),Times.Exactly(2));
    }
    [Fact]
    public async Task DisablingAreaSuppressesAnAlreadyQueuedRetry()
    {
        var(db,connection)=TestDb.Create();await using var d=db;await using var c=connection;var clock=new Clock();
        await SetupAsync(db);await EnqueueAsync(db,clock);var sender=Sender(0);var worker=Processor(db,clock,sender.Object,new(){RetryMinutes=1});
        await worker.ProcessAsync();var subscription=await db.ResidentZoneSubscriptions.SingleAsync();subscription.AlertsEnabled=false;await db.SaveChangesAsync();
        clock.Now=clock.Now.AddMinutes(1);await worker.ProcessAsync();Assert.Equal(RiskDeliveryState.Suppressed,(await db.RiskAlertDeliveries.SingleAsync()).State);
        sender.Verify(n=>n.SendToUserAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact]
    public async Task ConcurrentProcessorCannotClaimTheSameDelivery()
    {
        var(db,connection)=TestDb.Create();await using var d=db;await using var c=connection;var clock=new Clock();
        await SetupAsync(db);await EnqueueAsync(db,clock);
        var started=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);var sender=new Mock<INotificationService>();
        sender.Setup(n=>n.SendToUserAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<CancellationToken>()))
            .Returns(()=>{started.TrySetResult(true);return finish.Task;});
        var first=Processor(db,clock,sender.Object).ProcessAsync();await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await using var secondDb=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        Assert.Equal(0,await Processor(secondDb,clock,sender.Object).ProcessAsync());finish.SetResult(1);await first;
        Assert.Single(await db.RiskAlertDeliveries.ToListAsync());
        sender.Verify(n=>n.SendToUserAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact]
    public async Task ExpiredSendingLeaseRecoversAfterRestartWithoutAnotherReceipt()
    {
        var(db,connection)=TestDb.Create();await using var d=db;await using var c=connection;var clock=new Clock();
        await SetupAsync(db);var item=await EnqueueAsync(db,clock);item.ProcessedAtUtc=clock.Now;
        db.RiskAlertDeliveries.Add(new(){EventId=item.Id,ResidentId=1,ZoneId=item.RiskHistory.ZoneId,State=RiskDeliveryState.Sending,
            Attempts=1,CreatedAtUtc=clock.Now.AddMinutes(-3),LeaseUntilUtc=clock.Now.AddMinutes(-1),ClaimToken="old"});await db.SaveChangesAsync();
        await Processor(db,clock,Sender(1).Object).ProcessAsync();var receipt=await db.RiskAlertDeliveries.SingleAsync();
        Assert.Equal(2,receipt.Attempts);Assert.Equal(RiskDeliveryState.Sent,receipt.State);Assert.NotNull(receipt.SentAtUtc);
    }

    [Fact]
    public async Task ExpiredFinalAttemptIsVisibleAsFailedWithoutUnboundedRetry()
    {
        var(db,connection)=TestDb.Create();await using var d=db;await using var c=connection;var clock=new Clock();
        await SetupAsync(db);var item=await EnqueueAsync(db,clock);item.ProcessedAtUtc=clock.Now;
        db.RiskAlertDeliveries.Add(new(){EventId=item.Id,ResidentId=1,ZoneId=item.RiskHistory.ZoneId,State=RiskDeliveryState.Sending,
            Attempts=3,CreatedAtUtc=clock.Now.AddMinutes(-3),LeaseUntilUtc=clock.Now.AddMinutes(-1)});await db.SaveChangesAsync();
        var sender=Sender(1);await Processor(db,clock,sender.Object).ProcessAsync();var receipt=await db.RiskAlertDeliveries.SingleAsync();
        Assert.Equal(RiskDeliveryState.Failed,receipt.State);Assert.Contains("unconfirmed",receipt.LastError);
        sender.Verify(n=>n.SendToUserAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<DateTime?>(),It.IsAny<CancellationToken>()),Times.Never);
    }
}
