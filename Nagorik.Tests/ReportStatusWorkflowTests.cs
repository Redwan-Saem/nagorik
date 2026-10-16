
using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
using Xunit;

public class ReportStatusWorkflowTests
{
    
    [Theory]
    [InlineData(ReportStatus.Pending, ReportStatus.Approved)]
    [InlineData(ReportStatus.Pending, ReportStatus.Rejected)]
    [InlineData(ReportStatus.Approved, ReportStatus.Assigned)]
    [InlineData(ReportStatus.Approved, ReportStatus.Rejected)]
    [InlineData(ReportStatus.Assigned, ReportStatus.InProgress)]
    [InlineData(ReportStatus.InProgress, ReportStatus.Completed)]
    public void CanTransition_AllowsValidForwardTransitions(
        ReportStatus current,
        ReportStatus next)
    {
        Assert.True(
            ReportStatusWorkflowService.CanTransition(current, next));
    }

    [Theory]
    [InlineData(ReportStatus.Approved, ReportStatus.Pending)]
    [InlineData(ReportStatus.Assigned, ReportStatus.Approved)]
    [InlineData(ReportStatus.InProgress, ReportStatus.Assigned)]
    [InlineData(ReportStatus.Completed, ReportStatus.InProgress)]
    [InlineData(ReportStatus.Rejected, ReportStatus.Approved)]
    [InlineData(ReportStatus.Pending, ReportStatus.Completed)]
    [InlineData(ReportStatus.Approved, ReportStatus.Approved)]
    public void CanTransition_RejectsInvalidTransitions(
        ReportStatus current,
        ReportStatus next)
    {
        Assert.False(
            ReportStatusWorkflowService.CanTransition(current, next));
    }
    [Fact]
public async Task ChangeStatusAsync_ValidTransition_SavesHistory()
{
    var (db, connection) = TestDb.Create();
await using var _connection = connection;
await using var _db = db;

    TestDb.AddUser(db, 1);

    db.Reports.Add(new Report
    {
        Id = 1,
        Title = "Broken road",
        Description = "Road needs repair",
        Category = ReportCategory.RoadDamage,
        Authority = Authority.CityCorporation,
        Status = ReportStatus.Pending,
        Location = "Dhaka",
        Latitude = 23.75,
        Longitude = 90.39,
        UserId = 1
    });

    await db.SaveChangesAsync();

    var service = new ReportStatusWorkflowService(db, ResidentNotificationTestHelper.Create());

    var result = await service.ChangeStatusAsync(
        1, ReportStatus.Approved, 1);

    Assert.Equal(StatusChangeOutcome.Changed, result.Outcome);

    db.ChangeTracker.Clear();

    var report = await db.Reports.SingleAsync(r => r.Id == 1);
    Assert.Equal(ReportStatus.Approved, report.Status);

    var history = await db.ReportStatusHistories
        .SingleAsync(h => h.ReportId == 1);

    Assert.Equal(ReportStatus.Approved, history.Status);
    Assert.True(history.ChangedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    Assert.True(history.ChangedAtUtc <= DateTime.UtcNow);
}

[Fact]
public async Task ChangeStatusAsync_InvalidTransition_DoesNotChangeDatabase()
{
    var (db, connection) = TestDb.Create();
await using var _connection = connection;
await using var _db = db;
    TestDb.AddUser(db, 1);

    db.Reports.Add(new Report
    {
        Id = 1,
        Title = "Broken road",
        Description = "Road needs repair",
        Category = ReportCategory.RoadDamage,
        Authority = Authority.CityCorporation,
        Status = ReportStatus.Pending,
        Location = "Dhaka",
        Latitude = 23.75,
        Longitude = 90.39,
        UserId = 1
    });

    await db.SaveChangesAsync();

    var service = new ReportStatusWorkflowService(db, ResidentNotificationTestHelper.Create());

    var result = await service.ChangeStatusAsync(
        1, ReportStatus.Completed, 1);

    Assert.Equal(
        StatusChangeOutcome.InvalidTransition,
        result.Outcome);

    db.ChangeTracker.Clear();

    var report = await db.Reports.SingleAsync(r => r.Id == 1);
    Assert.Equal(ReportStatus.Pending, report.Status);

    var historyCount = await db.ReportStatusHistories
        .CountAsync(h => h.ReportId == 1);

    Assert.Equal(0, historyCount);
}
}
