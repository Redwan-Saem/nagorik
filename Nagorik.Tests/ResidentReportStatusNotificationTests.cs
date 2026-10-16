using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class ResidentReportStatusNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulChange_NotifiesResidentOnceAfterSaving(bool assignment)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        var report = await Seed(db, assignment);
        var next = assignment ? ReportStatus.Assigned : ReportStatus.Approved;
        var sender = new Mock<INotificationService>(MockBehavior.Strict);
        sender.Setup(n => n.SendToUserAsync("1", "Report status updated",
                $"R-{report.Id:D6} status changed to {next}.", $"/Reports/Detail/{report.Id}",
                null, default))
            .Returns(async () =>
            {
                // A separate context confirms delivery sees committed data.
                await using var persisted = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite(connection).Options);
                Assert.Equal(next, (await persisted.Reports.SingleAsync()).Status);
                Assert.Equal(next, (await persisted.ReportStatusHistories.SingleAsync()).Status);
                Assert.Equal(assignment ? 1 : 0, await persisted.CrewAssignments.CountAsync());
                return 0; // No push subscription is also a successful status change.
            });

        var notifier = ResidentNotificationTestHelper.Create(sender.Object);
        await Change(db, report, assignment, notifier);
        await Change(db, report, assignment, notifier); // Repeated requests must not notify again.
        sender.VerifyAll();
        sender.Verify(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationFailure_DoesNotUndoChangeOrPreventCrewNotification(bool assignment)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        var report = await Seed(db, assignment);
        var sender = new Mock<INotificationService>();
        sender.Setup(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Push unavailable"));
        var crewNotifier = new Mock<ICrewTaskNotifier>();

        await Change(db, report, assignment, ResidentNotificationTestHelper.Create(sender.Object), crewNotifier.Object);

        db.ChangeTracker.Clear();
        Assert.Equal(assignment ? ReportStatus.Assigned : ReportStatus.Approved,
            (await db.Reports.SingleAsync()).Status);
        Assert.Single(await db.ReportStatusHistories.ToListAsync());
        if (assignment)
        {
            Assert.Single(await db.CrewAssignments.ToListAsync());
            crewNotifier.Verify(n => n.NotifyAssignedAsync(It.IsAny<Report>(), It.IsAny<Crew>()), Times.Once);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveFailure_DoesNotNotify(bool assignment)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        var report = await Seed(db, assignment);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_report_update BEFORE UPDATE ON Reports BEGIN SELECT RAISE(ABORT, 'test save failure'); END;");
        var sender = new Mock<INotificationService>(MockBehavior.Strict);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            Change(db, report, assignment, ResidentNotificationTestHelper.Create(sender.Object)));

        sender.VerifyNoOtherCalls();
        db.ChangeTracker.Clear();
        Assert.Equal(assignment ? ReportStatus.Approved : ReportStatus.Pending,
            (await db.Reports.SingleAsync()).Status);
        Assert.Empty(await db.ReportStatusHistories.ToListAsync());
        Assert.Empty(await db.CrewAssignments.ToListAsync());
    }

    [Fact]
    public async Task MissingAndInvalidChanges_DoNotNotify()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        var report = await Seed(db, false);
        var sender = new Mock<INotificationService>(MockBehavior.Strict);
        var workflow = new ReportStatusWorkflowService(db, ResidentNotificationTestHelper.Create(sender.Object), ResidentNotificationTestHelper.Dispatcher(db));
        Assert.Equal(StatusChangeOutcome.ReportNotFound,
            (await workflow.ChangeStatusAsync(999, ReportStatus.Approved, 1)).Outcome);
        Assert.Equal(StatusChangeOutcome.InvalidTransition,
            (await workflow.ChangeStatusAsync(report.Id, ReportStatus.Completed, 1)).Outcome);
        Assert.Equal(StatusChangeOutcome.InvalidTransition,
            (await workflow.ChangeStatusAsync(report.Id, ReportStatus.Pending, 1)).Outcome);
        sender.VerifyNoOtherCalls();
        Assert.Empty(await db.ReportStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task RealNotificationService_LogsResidentUpdateWithoutSubscription()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        var report = await Seed(db, false);
        var push = new Mock<IPushSender>(MockBehavior.Strict);
        var notifications = new NotificationService(db, push.Object);
        await Change(db, report, false, ResidentNotificationTestHelper.Create(notifications));
        var entry = await db.NotificationLogs.SingleAsync();
        Assert.Equal("1", entry.UserId);
        Assert.Equal($"/Reports/Detail/{report.Id}", entry.Url);
        Assert.Contains("Approved", entry.Body);
        Assert.False(entry.Success);
        push.VerifyNoOtherCalls();
    }

    private static async Task<Report> Seed(AppDbContext db, bool assignment)
    {
        TestDb.AddUser(db, 1);
        TestDb.AddUser(db, 2);
        db.Crews.Add(new Crew { Id = 1, UserId = 2, Name = "Crew" });
        var report = new Report
        {
            UserId = 1, Title = "Road damage", Authority = Authority.CityCorporation,
            Status = assignment ? ReportStatus.Approved : ReportStatus.Pending
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }

    private static async Task Change(AppDbContext db, Report report, bool assignment,
        ResidentReportStatusNotifier notifier, ICrewTaskNotifier? crewNotifier = null)
    {
        if (assignment)
        {
            var service = new CrewAssignmentService(db, crewNotifier ?? Mock.Of<ICrewTaskNotifier>(),
                NullLogger<CrewAssignmentService>.Instance, notifier);
            var result = await service.AssignAsync(report.Id, 1, 1, false);
            Assert.Contains(result.Outcome, new[] { AssignOutcome.Assigned, AssignOutcome.NotAssignable });
        }
        else
        {
            var result = await new ReportStatusWorkflowService(db, notifier, ResidentNotificationTestHelper.Dispatcher(db))
                .ChangeStatusAsync(report.Id, ReportStatus.Approved, 1);
            Assert.Contains(result.Outcome, new[] { StatusChangeOutcome.Changed, StatusChangeOutcome.InvalidTransition });
        }
    }
}
