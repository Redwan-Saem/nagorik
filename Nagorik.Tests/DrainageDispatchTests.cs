using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;
public class DrainageDispatchTests
{
    [Fact]
    public async Task RequiresBusyConfirmationAndReusesOneWorkReport()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        TestDb.AddUser(db, 1); TestDb.AddUser(db, 2);
        var zone = RiskPredictionTests.Zone(); zone.RiskLevel = ZoneRiskLevel.High; zone.LastComputedAtUtc = DateTime.UtcNow;
        var point = new DrainagePoint { Zone = zone, Latitude = 23.7, Longitude = 90.3 };
        var crew = new Crew { Name = "Busy", UserId = 2 }; db.Crews.Add(crew); db.DrainagePoints.Add(point);
        db.Reports.Add(new() { UserId = 1, AssignedCrew = crew, Status = ReportStatus.InProgress }); await db.SaveChangesAsync();
        var assignment = new CrewAssignmentService(db, new NullCrewTaskNotifier(), NullLogger<CrewAssignmentService>.Instance, ResidentNotificationTestHelper.Create());
        var service = new DrainageDispatchService(db, assignment, TimeProvider.System);
        Assert.Equal(AssignOutcome.NeedsConfirmation, (await service.AssignAsync(point.Id, crew.Id, 1, false)).Outcome);
        Assert.Equal(1, await db.Reports.CountAsync()); Assert.Null(point.LastWorkReportId);
        var first = await service.AssignAsync(point.Id, crew.Id, 1, true); Assert.Equal(AssignOutcome.Assigned, first.Outcome);
        var duplicate = await service.AssignAsync(point.Id, crew.Id, 1, true); Assert.Equal(AssignOutcome.Conflict, duplicate.Outcome);
        Assert.Equal(first.ReportId, duplicate.ReportId); Assert.Equal(2, await db.Reports.CountAsync());
        Assert.Single(await db.CrewAssignments.ToListAsync());
        var report = await db.Reports.SingleAsync(r => r.Id == first.ReportId); report.Status = ReportStatus.Completed; await db.SaveChangesAsync();
        zone.LastComputedAtUtc = DateTime.UtcNow.AddHours(-1); await db.SaveChangesAsync();
        Assert.Equal(AssignOutcome.NotAssignable, (await service.AssignAsync(point.Id, crew.Id, 1, true)).Outcome);
        Assert.Equal(2, await db.Reports.CountAsync());
    }
}
