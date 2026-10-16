using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class TaskStatusHistoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TaskLifecycle_RecordsActorsAndDerivesAvailability(bool otherActiveTask)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db, 1);
        TestDb.AddUser(db, 2);
        TestDb.AddUser(db, 3);
        db.Crews.Add(new Crew { Id = 1, UserId = 3, Name = "Crew" });
        var report = new Report { UserId = 1, Status = ReportStatus.Approved, Authority = Authority.CityCorporation };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        var notifier = ResidentNotificationTestHelper.Create();
        var assignments = new CrewAssignmentService(db, Mock.Of<ICrewTaskNotifier>(),
            NullLogger<CrewAssignmentService>.Instance, notifier);
        var workflow = new ReportStatusWorkflowService(db, notifier, ResidentNotificationTestHelper.Dispatcher(db));

        Assert.False((await assignments.GetCrewsAsync()).Single().IsBusy);
        Assert.Equal(AssignOutcome.Assigned, (await assignments.AssignAsync(report.Id, 1, 2, false)).Outcome);
        Assert.True((await assignments.GetCrewsAsync()).Single().IsBusy);
        Assert.Equal(AssignOutcome.NotAssignable, (await assignments.AssignAsync(report.Id, 1, 2, true)).Outcome);
        Assert.Equal(StatusChangeOutcome.Changed,
            (await workflow.ChangeStatusAsync(report.Id, ReportStatus.InProgress, 3)).Outcome);
        Assert.True((await assignments.GetCrewsAsync()).Single().IsBusy);

        if (otherActiveTask)
        {
            db.Reports.Add(new Report { UserId = 1, AssignedCrewId = 1, Status = ReportStatus.Assigned });
            await db.SaveChangesAsync();
        }

        Assert.Equal(StatusChangeOutcome.Changed,
            (await workflow.ChangeStatusAsync(report.Id, ReportStatus.Completed, 3)).Outcome);
        Assert.Equal(otherActiveTask, (await assignments.GetCrewsAsync()).Single().IsBusy);
        Assert.Equal(StatusChangeOutcome.InvalidTransition,
            (await workflow.ChangeStatusAsync(report.Id, ReportStatus.Completed, 3)).Outcome);

        db.ChangeTracker.Clear();
        var history = await db.ReportStatusHistories.Where(h => h.ReportId == report.Id)
            .OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id).ToListAsync();
        Assert.Equal(new[] { ReportStatus.Assigned, ReportStatus.InProgress, ReportStatus.Completed }, history.Select(h => h.Status));
        Assert.Equal(new ReportStatus?[] { ReportStatus.Approved, ReportStatus.Assigned, ReportStatus.InProgress }, history.Select(h => h.PreviousStatus));
        Assert.Equal(new int?[] { 2, 3, 3 }, history.Select(h => h.ChangedByUserId));
        var assignment = await db.CrewAssignments.SingleAsync();
        Assert.Equal(assignment.DispatcherId, history[0].ChangedByUserId);
        Assert.Equal(assignment.AssignedAtUtc, history[0].ChangedAtUtc);
        Assert.All(history, h => Assert.InRange(h.ChangedAtUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow));
    }

    [Fact]
    public async Task UnknownActor_RejectsHistoryAndRollsBackStatus()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = new Report { UserId = 1, Status = ReportStatus.Assigned };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        var sender = new Mock<INotificationService>(MockBehavior.Strict);
        var workflow = new ReportStatusWorkflowService(db, ResidentNotificationTestHelper.Create(sender.Object), ResidentNotificationTestHelper.Dispatcher(db));
        await Assert.ThrowsAsync<DbUpdateException>(() => workflow.ChangeStatusAsync(report.Id, ReportStatus.InProgress, 999));
        db.ChangeTracker.Clear();
        Assert.Equal(ReportStatus.Assigned, (await db.Reports.SingleAsync()).Status);
        Assert.Empty(await db.ReportStatusHistories.ToListAsync());
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Migration_PreservesLegacyHistoryAndSupportsRollback()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261016184513_AddReportStatusHistory");
        TestDb.AddUser(db);
        await TestDb.AddLegacyReportAsync(db);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO ReportStatusHistories (ReportId, Status, ChangedAtUtc) VALUES (1, 'Pending', '2026-10-17 00:00:00');");

        await migrator.MigrateAsync();
        var legacy = await db.ReportStatusHistories.AsNoTracking().SingleAsync();
        Assert.Equal(ReportStatus.Pending, legacy.Status);
        Assert.Null(legacy.PreviousStatus);
        Assert.Null(legacy.ChangedByUserId);
        Assert.False(db.Database.HasPendingModelChanges());

        await migrator.MigrateAsync("20261016184513_AddReportStatusHistory");
        await migrator.MigrateAsync();
        Assert.Equal(legacy.ChangedAtUtc, (await db.ReportStatusHistories.AsNoTracking().SingleAsync()).ChangedAtUtc);
    }
}
