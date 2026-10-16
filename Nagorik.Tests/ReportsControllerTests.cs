using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Controllers;
using Nagorik.Api.Models;
using Xunit;

public class ReportsControllerTests
{
    [Fact]
    public async Task Detail_ReturnsOwnedReportWithOrderedHistory()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = new Report { UserId = 1, Title = "Flooded road" };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        var now = DateTime.UtcNow;
        db.ReportStatusHistories.AddRange(
            new ReportStatusHistory { ReportId = report.Id, Status = ReportStatus.Approved, ChangedAtUtc = now },
            new ReportStatusHistory { ReportId = report.Id, Status = ReportStatus.Pending, ChangedAtUtc = now.AddHours(-1) });
        await db.SaveChangesAsync();

        var controller = Controller(db, "1");
        var result = Assert.IsType<ViewResult>(await controller.Detail(report.Id, default));
        var model = Assert.IsType<ReportDetailViewModel>(result.Model);
        Assert.Equal("Flooded road", model.Report.Title);
        Assert.Equal(new[] { ReportStatus.Pending, ReportStatus.Approved }, model.StatusHistory.Select(h => h.Status));
        Assert.Equal(2, db.ReportStatusHistories.Count());
    }

    [Theory]
    [InlineData("2", false)]
    [InlineData("invalid", false)]
    [InlineData("1", true)]
    public async Task Detail_HidesOtherResidentsAndMissingReports(string identity, bool missing)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = new Report { UserId = 1 };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await Controller(db, identity).Detail(missing ? report.Id + 1 : report.Id, default));
        Assert.Empty(db.ReportStatusHistories);
    }

    [Fact]
    public async Task Detail_SupportsEmptyHistory()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = new Report { UserId = 1 };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        var result = Assert.IsType<ViewResult>(await Controller(db, "1").Detail(report.Id, default));
        Assert.Empty(Assert.IsType<ReportDetailViewModel>(result.Model).StatusHistory);
    }

    private static ReportsController Controller(Nagorik.Api.AppDbContext db, string identity)
    {
        return new ReportsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, identity),
                        new Claim(ClaimTypes.Role, "Resident")
                    }, "Test"))
                }
            }
        };
    }
}
