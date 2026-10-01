using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class CrewAssignmentServiceTests
{
    private class TestNotifier : ICrewTaskNotifier
    {
        public int CallCount { get; private set; }
        public int? ReportId { get; private set; }
        public int? CrewId { get; private set; }
        public bool ThrowOnCall { get; set; }

        public Task NotifyAssignedAsync(Report report, Crew crew)
        {
            CallCount++;
            ReportId = report.Id;
            CrewId = crew.Id;

            if (ThrowOnCall)
                throw new Exception("Notification failed.");

            return Task.CompletedTask;
        }
    }

    private static CrewAssignmentService CreateService(
        AppDbContext db,
        TestNotifier notifier)
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<CrewAssignmentService>();

        return new CrewAssignmentService(
            db,
            notifier,
            logger);
    }

    private static Report AddAssignableReport(
        AppDbContext db,
        int id = 1)
    {
        var report = new Report
        {
            Id = id,
            Title = "Road problem",
            Description = "Test report",
            Category = ReportCategory.RoadDamage,
            Authority = Authority.CityCorporation,
            Status = ReportStatus.Approved,
            Location = "Dhaka",
            Latitude = 23.75,
            Longitude = 90.39,
            UserId = 1
        };

        db.Reports.Add(report);
        db.SaveChanges();

        return report;
    }

    private static Crew AddCrew(
        AppDbContext db,
        int id = 1)
    {
        var crew = new Crew
        {
            Id = id,
            Name = $"Crew {id}",
            UserId = id + 100
        };

        db.Crews.Add(crew);
        db.SaveChanges();

        return crew;
    }

    [Fact]
    public async Task AssignAsync_AssignsCrewAndCreatesAssignmentLog()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db);
            var crew = AddCrew(db);

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                crew.Id,
                1,
                false);

            Assert.Equal(AssignOutcome.Assigned, result.Outcome);

            var updatedReport = await db.Reports
                .SingleAsync(r => r.Id == report.Id);

            Assert.Equal(crew.Id, updatedReport.AssignedCrewId);
            Assert.Equal(ReportStatus.Assigned, updatedReport.Status);

            var assignment = await db.CrewAssignments
                .SingleAsync();

            Assert.Equal(report.Id, assignment.ReportId);
            Assert.Equal(crew.Id, assignment.CrewId);
            Assert.Equal(1, assignment.DispatcherId);

            Assert.Equal(1, notifier.CallCount);
            Assert.Equal(report.Id, notifier.ReportId);
            Assert.Equal(crew.Id, notifier.CrewId);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_ReturnsNeedsConfirmation_WhenCrewIsBusy()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db, 1);
            var activeReport = AddAssignableReport(db, 2);
            var crew = AddCrew(db);

            activeReport.AssignedCrewId = crew.Id;
            activeReport.Status = ReportStatus.InProgress;
            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                crew.Id,
                1,
                false);

            Assert.Equal(
                AssignOutcome.NeedsConfirmation,
                result.Outcome);

            Assert.Contains("already working", result.Message);

            var unchangedReport = await db.Reports
                .SingleAsync(r => r.Id == report.Id);

            Assert.Null(unchangedReport.AssignedCrewId);
            Assert.Equal(ReportStatus.Approved, unchangedReport.Status);

            Assert.Empty(await db.CrewAssignments.ToListAsync());
            Assert.Equal(0, notifier.CallCount);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_AssignsBusyCrew_WhenConfirmationIsTrue()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db, 1);
            var activeReport = AddAssignableReport(db, 2);
            var crew = AddCrew(db);

            activeReport.AssignedCrewId = crew.Id;
            activeReport.Status = ReportStatus.InProgress;
            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                crew.Id,
                1,
                true);

            Assert.Equal(AssignOutcome.Assigned, result.Outcome);

            var updatedReport = await db.Reports
                .SingleAsync(r => r.Id == report.Id);

            Assert.Equal(crew.Id, updatedReport.AssignedCrewId);
            Assert.Equal(ReportStatus.Assigned, updatedReport.Status);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_CompletedReportDoesNotMakeCrewBusy()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db, 1);
            var completedReport = AddAssignableReport(db, 2);
            var crew = AddCrew(db);

            completedReport.AssignedCrewId = crew.Id;
            completedReport.Status = ReportStatus.Completed;
            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                crew.Id,
                1,
                false);

            Assert.Equal(AssignOutcome.Assigned, result.Outcome);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetCrewsAsync_MarksCrewBusyForAssignedStatus()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var crew = AddCrew(db);
            var report = AddAssignableReport(db);

            report.AssignedCrewId = crew.Id;
            report.Status = ReportStatus.Assigned;

            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var crews = await service.GetCrewsAsync();

            var result = Assert.Single(crews);

            Assert.True(result.IsBusy);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetCrewsAsync_MarksCrewBusyForInProgressStatus()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var crew = AddCrew(db);
            var report = AddAssignableReport(db);

            report.AssignedCrewId = crew.Id;
            report.Status = ReportStatus.InProgress;

            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var crews = await service.GetCrewsAsync();

            var result = Assert.Single(crews);

            Assert.True(result.IsBusy);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetCrewsAsync_DoesNotMarkCrewBusyForCompletedReport()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var crew = AddCrew(db);
            var report = AddAssignableReport(db);

            report.AssignedCrewId = crew.Id;
            report.Status = ReportStatus.Completed;

            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var crews = await service.GetCrewsAsync();

            var result = Assert.Single(crews);

            Assert.False(result.IsBusy);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_ReturnsReportNotFound()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            var crew = AddCrew(db);

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                999,
                crew.Id,
                1,
                false);

            Assert.Equal(
                AssignOutcome.ReportNotFound,
                result.Outcome);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_ReturnsCrewNotFound()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db);

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                999,
                1,
                false);

            Assert.Equal(
                AssignOutcome.CrewNotFound,
                result.Outcome);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_ReturnsNotAssignableForAlreadyAssignedReport()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db);
            var oldCrew = AddCrew(db, 1);
            var newCrew = AddCrew(db, 2);

            report.AssignedCrewId = oldCrew.Id;
            report.Status = ReportStatus.Assigned;

            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                newCrew.Id,
                1,
                false);

            Assert.Equal(
                AssignOutcome.NotAssignable,
                result.Outcome);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_ReturnsNotAssignableForNonCityCorporationReport()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db);
            var crew = AddCrew(db);

            report.Authority = Authority.Police;
            await db.SaveChangesAsync();

            var notifier = new TestNotifier();
            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                crew.Id,
                1,
                false);

            Assert.Equal(
                AssignOutcome.NotAssignable,
                result.Outcome);
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }

    [Fact]
    public async Task AssignAsync_DoesNotFailWhenNotifierThrows()
    {
        var (db, conn) = TestDb.Create();

        try
        {
            TestDb.AddUser(db, 1);

            var report = AddAssignableReport(db);
            var crew = AddCrew(db);

            var notifier = new TestNotifier
            {
                ThrowOnCall = true
            };

            var service = CreateService(db, notifier);

            var result = await service.AssignAsync(
                report.Id,
                crew.Id,
                1,
                false);

            Assert.Equal(
                AssignOutcome.Assigned,
                result.Outcome);

            Assert.Equal(1, notifier.CallCount);

            var updatedReport = await db.Reports
                .SingleAsync(r => r.Id == report.Id);

            Assert.Equal(crew.Id, updatedReport.AssignedCrewId);
            Assert.Equal(ReportStatus.Assigned, updatedReport.Status);

            Assert.Single(await db.CrewAssignments.ToListAsync());
        }
        finally
        {
            await db.DisposeAsync();
            await conn.DisposeAsync();
        }
    }
}