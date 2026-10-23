using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;
public class CrewLocationWorkflowTests
{
    [Fact]
    public async Task LocationIsSavedOnlyWithSuccessfulOwnedTaskTransition()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        TestDb.AddUser(db, 1); TestDb.AddUser(db, 2);
        var crew = new Crew { UserId = 1, Name = "One" }; db.Crews.Add(crew);
        var report = new Report { UserId = 2, AssignedCrew = crew, Status = ReportStatus.Assigned };
        db.Reports.Add(report); await db.SaveChangesAsync();
        var service = new ReportStatusWorkflowService(db, ResidentNotificationTestHelper.Create(), ResidentNotificationTestHelper.Dispatcher(db));
        var location = new CrewLocationUpdate { Latitude = 23.75, Longitude = 90.39 };
        Assert.Equal(StatusChangeOutcome.ReportNotFound,
            (await service.ChangeCrewTaskStatusAsync(report.Id, ReportStatus.InProgress, 2, location: location)).Outcome);
        Assert.Null(crew.LastLatitude);
        Assert.Equal(StatusChangeOutcome.InvalidTransition,
            (await service.ChangeCrewTaskStatusAsync(report.Id, ReportStatus.Completed, 1, location: location)).Outcome);
        Assert.Null(crew.LastLatitude);
        Assert.Equal(StatusChangeOutcome.Changed,
            (await service.ChangeCrewTaskStatusAsync(report.Id, ReportStatus.InProgress, 1, location: location)).Outcome);
        db.ChangeTracker.Clear(); var saved = await db.Crews.SingleAsync();
        Assert.Equal(23.75, saved.LastLatitude); Assert.Equal(90.39, saved.LastLongitude); Assert.NotNull(saved.LastSeenAtUtc);
        Assert.Equal(StatusChangeOutcome.Changed,
            (await service.ChangeCrewTaskStatusAsync(report.Id, ReportStatus.Completed, 1)).Outcome);
        Assert.Equal(saved.LastSeenAtUtc, (await db.Crews.AsNoTracking().SingleAsync()).LastSeenAtUtc);
    }
}
