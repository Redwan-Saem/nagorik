using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Data;
using Nagorik.Api.Models;

public class ReportFilterSchemaTests
{
    [Fact]
    public async Task LookupDataMatchesCategoriesAndSupportsNullAreaAndEveryUrgency()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        Assert.Equal(Enum.GetValues<ReportCategory>(), ReportLookups.Categories.Select(c => c.Category));
        Assert.All(ReportLookups.Categories, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.Equal(new[] { "Dhanmondi", "Mirpur", "Gulshan" }, await db.ReportAreas.OrderBy(a => a.Id).Select(a => a.Name).ToArrayAsync());
        foreach (var urgency in Enum.GetValues<ReportUrgency>())
            db.Reports.Add(new Report { UserId = 1, AreaId = 1, Urgency = urgency });
        db.Reports.Add(new Report { UserId = 1 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var reports = await db.Reports.Include(r => r.Area).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(Enum.GetValues<ReportUrgency>(), reports.Take(4).Select(r => r.Urgency));
        Assert.Equal("Dhanmondi", reports[0].Area!.Name);
        Assert.Null(reports.Last().AreaId);
        Assert.Equal(ReportUrgency.Normal, reports.Last().Urgency);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidAreaOrUrgencyIsRejected(bool area)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        db.Reports.Add(new Report { UserId = 1, AreaId = area ? 999 : null, Urgency = area ? ReportUrgency.Normal : (ReportUrgency)999 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.Reports.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("Production", true, true)]
    [InlineData("Staging", true, true)]
    [InlineData("Development", false, false)]
    public async Task FixtureSeedingRequiresDevelopmentAndExplicitFlags(string environment, bool sample, bool performance)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        Assert.Equal(0, await DevelopmentReportSeeder.SeedAsync(db, Environment(environment), Configuration(sample, performance)));
        Assert.Empty(await db.Reports.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task SamplesAreVariedAndRepeatableWithoutDuplicates()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        var environment = Environment("Development");
        var flags = Configuration(true, false);
        Assert.Equal(84, await DevelopmentReportSeeder.SeedAsync(db, environment, flags));
        Assert.Equal(0, await DevelopmentReportSeeder.SeedAsync(db, environment, flags));
        Assert.Equal(84, await db.Reports.CountAsync());
        Assert.Equal(7, await db.Reports.Select(r => r.Category).Distinct().CountAsync());
        Assert.Equal(3, await db.Reports.Select(r => r.AreaId).Distinct().CountAsync());
        Assert.Equal(6, await db.Reports.Select(r => r.Status).Distinct().CountAsync());
        Assert.Equal(4, await db.Reports.Select(r => r.Urgency).Distinct().CountAsync());
        Assert.All(await db.Reports.Where(r => r.Status == ReportStatus.Assigned || r.Status == ReportStatus.InProgress)
            .ToListAsync(), r => Assert.NotNull(r.AssignedCrewId));
        Assert.Empty(await db.NotificationLogs.ToListAsync());
    }

    [Fact]
    public async Task PerformanceFlagSeeds10000ReportsAndFilteredQueryUsesIndex()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        var environment = Environment("Development");
        var flags = Configuration(false, true);
        Assert.Equal(10_000, await DevelopmentReportSeeder.SeedAsync(db, environment, flags));
        Assert.Equal(10_000, await db.Reports.CountAsync());
        Assert.Equal(0, await DevelopmentReportSeeder.SeedAsync(db, environment, flags));
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT Id FROM Reports WHERE AreaId = 1 AND Category = 'RoadDamage' ORDER BY CreatedAt DESC, Id DESC LIMIT 20;";
        await using var reader = await command.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        Assert.Contains(plan, p => p.Contains("IX_Reports_AreaId_Category_CreatedAt_Id"));
        Assert.DoesNotContain(plan, p => p.Contains("TEMP B-TREE"));
    }

    [Fact]
    public async Task MigrationPreservesExistingReportsAndHistoryWithNormalUrgencyAndUnknownArea()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261016210535_AddSosSubmissionReference");
        TestDb.AddUser(db);
        await TestDb.AddLegacyReportAsync(db, title: "Existing report");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO ReportStatusHistories (ReportId, Status, ChangedAtUtc) VALUES (1, 'Pending', '2026-10-17 00:00:00');");
        await migrator.MigrateAsync();
        var report = await db.Reports.AsNoTracking().SingleAsync();
        Assert.Equal("Existing report", report.Title);
        Assert.Null(report.AreaId);
        Assert.Equal(ReportUrgency.Normal, report.Urgency);
        Assert.Single(await db.ReportStatusHistories.AsNoTracking().ToListAsync());
        Assert.Equal(3, await db.ReportAreas.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await migrator.MigrateAsync("20261016210535_AddSosSubmissionReference");
        await migrator.MigrateAsync();
        Assert.Equal("Existing report", (await db.Reports.AsNoTracking().SingleAsync()).Title);
        Assert.Single(await db.ReportStatusHistories.AsNoTracking().ToListAsync());
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(name);
        return environment.Object;
    }
    private static IConfiguration Configuration(bool sample, bool performance) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentData:SeedSampleReports"] = sample.ToString(),
            ["DevelopmentData:SeedPerformanceReports"] = performance.ToString()
        }).Build();
}
