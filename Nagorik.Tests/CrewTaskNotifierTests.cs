using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
public class CrewTaskNotifierTests
{
    private static Mock<INotificationService> CreateSender(int delivered = 1)
    {
        var mock = new Mock<INotificationService>();
        mock.Setup(n => n.SendToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivered);
        return mock;
    }

    private static CrewTaskNotifier CreateNotifier(INotificationService sender) =>
        new(sender, NullLogger<CrewTaskNotifier>.Instance);

    private static Report MakeReport(int id = 7, string location = "Road 5, Dhanmondi") => new()
    {
        Id = id,
        Title = "RoadDamage",
        Description = "x",
        Category = ReportCategory.RoadDamage,
        Authority = Authority.CityCorporation,
        Status = ReportStatus.Assigned,
        Location = location,
        UserId = 1
    };

    private static Crew MakeCrew(int userId = 105) =>
        new() { Id = 1, Name = "Crew 1", UserId = userId };

    private static async Task WithDb(Func<AppDbContext, Task> test)
    {
        var (db, conn) = TestDb.Create();
        try { await test(db); }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task SendsToTheCrewsUser_WithTitleAndUrl()
    {
        var sender = CreateSender();
        var notifier = CreateNotifier(sender.Object);

        await notifier.NotifyAssignedAsync(MakeReport(7), MakeCrew(105));

        sender.Verify(n => n.SendToUserAsync(
            "105",
            CrewTaskNotifier.Title,
            It.IsAny<string>(),
            "/crew/tasks/7",
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Body_ContainsCodeCategoryAndLocation()
    {
        var body = CrewTaskNotifier.BuildBody(MakeReport(7, "Road 5, Dhanmondi"));

        Assert.Contains("R-000007", body);
        Assert.Contains("RoadDamage", body);
        Assert.Contains("Road 5, Dhanmondi", body);
    }

    [Fact]
    public void Body_IsTruncated_WhenLocationIsVeryLong()
    {
        var body = CrewTaskNotifier.BuildBody(MakeReport(7, new string('a', 500)));

        Assert.Equal(CrewTaskNotifier.MaxBodyLength, body.Length);
        Assert.EndsWith("...", body);
    }

    [Fact]
    public void Body_HandlesMissingLocation()
    {
        var body = CrewTaskNotifier.BuildBody(MakeReport(7, "   "));

        Assert.Contains("location not provided", body);
    }

    [Fact]
    public async Task DoesNotThrow_WhenNoDeviceReceivedTheMessage()
    {
        var notifier = CreateNotifier(CreateSender(delivered: 0).Object);

        var ex = await Record.ExceptionAsync(
            () => notifier.NotifyAssignedAsync(MakeReport(), MakeCrew()));

        Assert.Null(ex);
    }

    [Fact]
    public async Task LetsSenderExceptionsBubbleUp_SoTheCallerCanLogThem()
    {
        var sender = new Mock<INotificationService>();
        sender.Setup(n => n.SendToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("push down"));

        var notifier = CreateNotifier(sender.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => notifier.NotifyAssignedAsync(MakeReport(), MakeCrew()));
    }

    [Fact]
    public async Task AssignAsync_NotifiesTheCrewsUser()
    {
        await WithDb(async db =>
        {
            TestDb.AddUser(db, 1);
            TestDb.AddUser(db, 101);

            db.Reports.Add(new Report
            {
                Id = 1,
                Title = "Road",
                Description = "x",
                Category = ReportCategory.RoadDamage,
                Authority = Authority.CityCorporation,
                Status = ReportStatus.Approved,
                Location = "Dhaka",
                Latitude = 23.75,
                Longitude = 90.39,
                UserId = 1
            });
            db.Crews.Add(new Crew { Id = 1, Name = "Crew 1", UserId = 101 });
            await db.SaveChangesAsync();

            var sender = CreateSender();
            var service = new CrewAssignmentService(
                db,
                CreateNotifier(sender.Object),
                NullLogger<CrewAssignmentService>.Instance, ResidentNotificationTestHelper.Create());

            var result = await service.AssignAsync(1, 1, 1, false);

            Assert.Equal(AssignOutcome.Assigned, result.Outcome);
            sender.Verify(n => n.SendToUserAsync(
                "101", It.IsAny<string>(), It.IsAny<string>(), "/crew/tasks/1",
                It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Fact]
    public async Task AssignAsync_StillSucceeds_WhenPushSendingThrows()
    {
        await WithDb(async db =>
        {
            TestDb.AddUser(db, 1);
            TestDb.AddUser(db, 101);

            db.Reports.Add(new Report
            {
                Id = 1,
                Title = "Road",
                Description = "x",
                Category = ReportCategory.RoadDamage,
                Authority = Authority.CityCorporation,
                Status = ReportStatus.Approved,
                Location = "Dhaka",
                Latitude = 23.75,
                Longitude = 90.39,
                UserId = 1
            });
            db.Crews.Add(new Crew { Id = 1, Name = "Crew 1", UserId = 101 });
            await db.SaveChangesAsync();

            var sender = new Mock<INotificationService>();
            sender.Setup(n => n.SendToUserAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("boom"));

            var service = new CrewAssignmentService(
                db,
                CreateNotifier(sender.Object),
                NullLogger<CrewAssignmentService>.Instance, ResidentNotificationTestHelper.Create());

            var result = await service.AssignAsync(1, 1, 1, false);

            Assert.Equal(AssignOutcome.Assigned, result.Outcome);
            Assert.Equal(ReportStatus.Assigned, (await db.Reports.SingleAsync(r => r.Id == 1)).Status);
            var history = await db.ReportStatusHistories
    .SingleAsync(h => h.ReportId == 1);

            Assert.Equal(ReportStatus.Assigned, history.Status);

            var assignment = await db.CrewAssignments
                .SingleAsync(a => a.ReportId == 1);

            Assert.Equal(assignment.AssignedAtUtc, history.ChangedAtUtc);
        });
    }
}