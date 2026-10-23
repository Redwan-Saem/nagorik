using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;
public class ZoneRiskAlertTests
{
    [Fact]
    public async Task ElevatedTransitionsAreDurableAndUnchangedRiskDoesNotRepeat()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var zone = RiskPredictionTests.Zone(); db.Zones.Add(zone);
        var resident = new User { Name = "Opted in", Role = "Resident" }; db.Users.Add(resident); await db.SaveChangesAsync();
        db.AreaSubscriptions.Add(new() { UserId = resident.Id.ToString(), ZoneId = zone.Code });
        db.NotificationPreferences.Add(new() { UserId = resident.Id.ToString(), NotificationsEnabled = true });
        var now = DateTime.UtcNow; var hour = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerHour, DateTimeKind.Utc);
        foreach (var forecast in new[] { false, true }) db.RainfallReadings.Add(new() { ZoneId = zone.Id, TimestampUtc = hour,
            FetchedAtUtc = now, IsForecast = forecast, Rainfall1HourMm = 20, Rainfall3HourMm = 60, Rainfall6HourMm = 120 });
        await db.SaveChangesAsync();
        var job = new RiskComputationService(db, new(db, TimeProvider.System, Options.Create(new WeatherOptions())), new(),
            new(Options.Create(new RiskScoringOptions())), TimeProvider.System);
        await job.ComputeAsync(); await job.ComputeAsync();
        Assert.Single(await db.RiskAlertEvents.ToListAsync()); Assert.Equal(2, await db.RiskHistories.CountAsync());
        var notify = new Mock<INotificationService>();
        notify.Setup(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var processor = new ZoneRiskAlertService(db, new(db), notify.Object, TimeProvider.System, NullLogger<ZoneRiskAlertService>.Instance);
        Assert.Equal(1, await processor.ProcessAsync()); Assert.Equal(0, await processor.ProcessAsync());
        notify.Verify(n => n.SendToUserAsync(resident.Id.ToString(), It.IsAny<string>(), It.Is<string>(s => s.Contains("Rule-based")),
            It.Is<string>(s => s.StartsWith("/map?zone=")), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull((await db.RiskAlertEvents.SingleAsync()).ProcessedAtUtc);
    }
}
