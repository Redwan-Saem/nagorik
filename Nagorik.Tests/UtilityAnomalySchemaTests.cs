using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class UtilityAnomalySchemaTests
{
    [Theory]
    [InlineData(AnomalyType.ExposedWire, true)]
    [InlineData(AnomalyType.GasLeak, true)]
    [InlineData(AnomalyType.FallenPole, true)]
    [InlineData(AnomalyType.PowerOutage, false)]
    [InlineData(AnomalyType.WaterLeak, false)]
    [InlineData(AnomalyType.Other, false)]
    public async Task AnomalyTypesRoundTripWithServerRiskMapping(AnomalyType type, bool highRisk)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        Assert.Equal(highRisk, UtilityAnomalyRules.IsHighRisk(type));
        Assert.Equal(highRisk ? ReportUrgency.High : ReportUrgency.Normal, UtilityAnomalyRules.GetUrgency(type));
        var report = Anomaly(type);
        report.PhotoPaths = ["/uploads/reports/one.jpg", "/uploads/reports/two.png", "/uploads/reports/three.jpg"];
        db.Reports.Add(report); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var stored = await db.Reports.SingleAsync();
        Assert.Equal(type, stored.AnomalyType); Assert.Equal(UtilityProvider.Unassigned, stored.Utility);
        Assert.Equal(report.Urgency, stored.Urgency); Assert.Equal("UA-000123", stored.ReferenceNo);
        Assert.Equal(report.PhotoPaths, stored.PhotoPaths);
        Assert.Equal(23.81, stored.Latitude); Assert.Equal(90.41, stored.Longitude);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT AnomalyType FROM Reports;";
        Assert.Equal(type.ToString(), await command.ExecuteScalarAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public void UnknownAnomalyTypesCannotBeClassifiedAsNormalRisk(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UtilityAnomalyRules.IsHighRisk((AnomalyType)value));
        Assert.Throws<ArgumentOutOfRangeException>(() => UtilityAnomalyRules.GetUrgency((AnomalyType)value));
    }

    [Theory]
    [InlineData(UtilityProvider.Unassigned)]
    [InlineData(UtilityProvider.Desco)]
    [InlineData(UtilityProvider.Dpdc)]
    [InlineData(UtilityProvider.WaterAuthority)]
    [InlineData(UtilityProvider.GasAuthority)]
    public async Task ProviderNamesRoundTrip(UtilityProvider provider)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = Anomaly(AnomalyType.Other); report.Utility = provider;
        db.Reports.Add(report); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(provider, (await db.Reports.SingleAsync()).Utility);
    }

    [Fact]
    public async Task GeneralReportsRetainSinglePhotoAndNullableDefaults()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        db.Reports.AddRange(new Report { UserId = 1, ImageUrl = "/uploads/reports/legacy.jpg" }, new Report { UserId = 1 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var reports = await db.Reports.OrderBy(r => r.Id).ToListAsync();
        Assert.All(reports, r =>
        {
            Assert.Null(r.AnomalyType); Assert.Null(r.Utility); Assert.Null(r.ReferenceNo);
            Assert.Empty(r.PhotoPaths); Assert.Equal(ReportUrgency.Normal, r.Urgency);
        });
        Assert.Equal("/uploads/reports/legacy.jpg", reports[0].ImageUrl);
    }

    [Fact]
    public async Task PhotoCollectionTracksInPlaceChangesWithoutSharingLists()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = Anomaly(AnomalyType.WaterLeak); report.PhotoPaths.Add("/uploads/reports/one.jpg");
        db.Reports.AddRange(report, new Report { UserId = 1 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var tracked = await db.Reports.SingleAsync(r => r.Id == report.Id);
        tracked.PhotoPaths[0] = "/uploads/reports/replaced.jpg";
        tracked.PhotoPaths.Add("/uploads/reports/two.png");
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(new[] { "/uploads/reports/replaced.jpg", "/uploads/reports/two.png" },
            (await db.Reports.SingleAsync(r => r.Id == report.Id)).PhotoPaths);
        Assert.Empty((await db.Reports.SingleAsync(r => r.Id != report.Id)).PhotoPaths);
    }

    [Fact]
    public async Task ReferenceUniquenessIsEnforcedWhileLegacyReferencesMayRemainNull()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        db.Reports.AddRange(new Report { UserId = 1 }, new Report { UserId = 1 }, Anomaly(AnomalyType.WaterLeak));
        await db.SaveChangesAsync(); db.Reports.Add(Anomaly(AnomalyType.PowerOutage));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(3, await db.Reports.CountAsync());
        Assert.Equal(2, await db.Reports.CountAsync(r => r.ReferenceNo == null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UA-12345")]
    [InlineData("ua-000123")]
    [InlineData("UA-00012X")]
    [InlineData("UA-000123 ")]
    [InlineData("UA-111111111111111111111111111111")]
    public async Task InvalidReferenceShapesAreRejected(string reference)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = Anomaly(AnomalyType.Other); report.ReferenceNo = reference;
        db.Reports.Add(report);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.Reports.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[10]")]
    [InlineData("[true]")]
    [InlineData("[\"\"]")]
    [InlineData("[\" \\t \"]")]
    [InlineData("[\"one.jpg\",null]")]
    [InlineData("[\"one.jpg\",\"two.jpg\",\"three.jpg\",\"four.jpg\"]")]
    public async Task PhotoJsonRejectsMalformedNonStringBlankOrExcessPaths(string json)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db); db.Reports.Add(new Report { UserId = 1 }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Reports SET PhotoPaths = {json};"));
        db.ChangeTracker.Clear();
        Assert.Empty((await db.Reports.SingleAsync()).PhotoPaths);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UndefinedAnomalyAndProviderValuesAreRejected(bool anomaly)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = Anomaly(AnomalyType.Other);
        if (anomaly) report.AnomalyType = (AnomalyType)999;
        else report.Utility = (UtilityProvider)999;
        db.Reports.Add(report);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(AnomalyType.ExposedWire, ReportUrgency.Low)]
    [InlineData(AnomalyType.ExposedWire, ReportUrgency.Normal)]
    [InlineData(AnomalyType.GasLeak, ReportUrgency.Low)]
    [InlineData(AnomalyType.GasLeak, ReportUrgency.Normal)]
    [InlineData(AnomalyType.FallenPole, ReportUrgency.Low)]
    [InlineData(AnomalyType.FallenPole, ReportUrgency.Normal)]
    public async Task HighRiskTypesCannotBeSavedBelowHighUrgency(AnomalyType type, ReportUrgency urgency)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = Anomaly(type); report.Urgency = urgency;
        db.Reports.Add(report);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(AnomalyType.ExposedWire)]
    [InlineData(AnomalyType.GasLeak)]
    [InlineData(AnomalyType.FallenPole)]
    public async Task HighRiskTypesAllowEscalationToCritical(AnomalyType type)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db);
        var report = Anomaly(type); report.Urgency = ReportUrgency.Critical;
        db.Reports.Add(report); await db.SaveChangesAsync();
        Assert.Equal(ReportUrgency.Critical, (await db.Reports.AsNoTracking().SingleAsync()).Urgency);
    }

    [Fact]
    public async Task MigrationPreservesLegacyPhotosCoordinatesAndRelationshipsAcrossRollback()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261016211342_AddReportAreaUrgencyAndFilterIndexes";
        await migrator.MigrateAsync(previous);
        TestDb.AddUser(db); TestDb.AddUser(db, 2);
        await TestDb.AddLegacyReportAsync(db, title: "Legacy with photo");
        await TestDb.AddLegacyReportAsync(db, id: 2, title: "Legacy without photo");
        await TestDb.AddLegacyReportAsync(db, id: 3, title: "Legacy blank photo");
        db.Crews.Add(new Crew { Id = 1, UserId = 2, Name = "Existing crew" }); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE Reports SET ImageUrl = '/uploads/reports/existing.jpg', AssignedCrewId = 1, Status = 'Assigned', Urgency = 3, AreaId = 1 WHERE Id = 1;");
        await db.Database.ExecuteSqlRawAsync("UPDATE Reports SET ImageUrl = '  ' WHERE Id = 3;");
        db.ReportStatusHistories.Add(new ReportStatusHistory { ReportId = 1, Status = ReportStatus.Assigned, ChangedByUserId = 1 });
        db.CrewAssignments.Add(new CrewAssignment { ReportId = 1, CrewId = 1, DispatcherId = 1, AssignedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        var report = await db.Reports.SingleAsync(r => r.Id == 1);
        Assert.Equal("Legacy with photo", report.Title);
        Assert.Equal(23.75, report.Latitude); Assert.Equal(90.39, report.Longitude);
        Assert.Equal("/uploads/reports/existing.jpg", report.ImageUrl);
        Assert.Equal(new[] { report.ImageUrl }, report.PhotoPaths);
        Assert.Null(report.AnomalyType); Assert.Null(report.Utility); Assert.Null(report.ReferenceNo);
        Assert.Equal(ReportStatus.Assigned, report.Status); Assert.Equal(ReportUrgency.Critical, report.Urgency);
        Assert.Equal(1, report.AreaId); Assert.Equal(1, report.AssignedCrewId);
        Assert.Empty((await db.Reports.SingleAsync(r => r.Id == 2)).PhotoPaths);
        Assert.Empty((await db.Reports.SingleAsync(r => r.Id == 3)).PhotoPaths);
        Assert.Single(await db.ReportStatusHistories.ToListAsync()); Assert.Single(await db.CrewAssignments.ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(3, (await new OfficialReportQueryService(db).ListAsync(new OfficialReportListQuery())).TotalCount);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA index_list('Reports');";
            await using var reader = await command.ExecuteReaderAsync();
            var names = new List<string>();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(1));
                if (reader.GetString(1) == "IX_Reports_ReferenceNo")
                {
                    Assert.Equal(1, reader.GetInt32(2)); Assert.Equal(1, reader.GetInt32(4));
                }
            }
            Assert.Contains("IX_Reports_ReferenceNo", names);
            Assert.Contains("IX_Reports_AnomalyType_Latitude_Longitude_CreatedAt", names);
            Assert.Contains("IX_Reports_Utility_CreatedAt_Id", names);
        }
        await migrator.MigrateAsync(previous); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(3, await db.Reports.CountAsync());
        Assert.Equal(new[] { "/uploads/reports/existing.jpg" }, (await db.Reports.SingleAsync(r => r.Id == 1)).PhotoPaths);
        Assert.Single(await db.ReportStatusHistories.ToListAsync()); Assert.Single(await db.CrewAssignments.ToListAsync());
        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA foreign_key_check;";
        await using var foreignKeys = await check.ExecuteReaderAsync();
        Assert.False(await foreignKeys.ReadAsync());
    }

    private static Report Anomaly(AnomalyType type) => new()
    {
        UserId = 1, Title = "Synthetic utility issue", Description = "Utility anomaly schema fixture.",
        Category = ReportCategory.Other, AnomalyType = type, Utility = UtilityProvider.Unassigned,
        ReferenceNo = "UA-000123", Urgency = UtilityAnomalyRules.GetUrgency(type),
        Location = "Dhaka", Latitude = 23.81, Longitude = 90.41
    };
}
