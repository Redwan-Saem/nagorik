using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class UtilityRoutingSchemaTests
{
    [Fact]
    public async Task SeedsIncludeGenericDefaultsAndInactiveElectricityTemplates()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var rules = await db.UtilityRoutingRules.AsNoTracking().ToListAsync();
        Assert.Equal(20, rules.Count); Assert.Equal(2, rules.Count(r => r.IsActive));
        Assert.Equal(18, rules.Count(r => !r.IsActive && r.AreaId.HasValue));
        Assert.All(rules.Where(r => r.Utility is UtilityProvider.Desco or UtilityProvider.Dpdc), r =>
        {
            Assert.False(r.IsActive); Assert.NotNull(r.AnomalyType); Assert.NotNull(r.AreaId);
            Assert.Null(r.MinLatitude); Assert.Contains("unverified template", r.Name);
        });
    }

    [Theory]
    [InlineData("name-empty")]
    [InlineData("name-long")]
    [InlineData("name-padded")]
    [InlineData("provider-unassigned")]
    [InlineData("provider-unknown")]
    [InlineData("type")]
    [InlineData("area")]
    [InlineData("priority-negative")]
    [InlineData("priority-excess")]
    [InlineData("partial-bounds")]
    [InlineData("latitude-range")]
    [InlineData("longitude-range")]
    [InlineData("reversed-latitude")]
    [InlineData("reversed-longitude")]
    public async Task DatabaseRejectsInvalidRuleConfiguration(string invalid)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var rule = new UtilityRoutingRule { Name = "Synthetic rule", Utility = UtilityProvider.Desco };
        switch (invalid)
        {
            case "name-empty": rule.Name = " \t "; break;
            case "name-long": rule.Name = new string('x', 161); break;
            case "name-padded": rule.Name = new string(' ', 160) + "x"; break;
            case "provider-unassigned": rule.Utility = UtilityProvider.Unassigned; break;
            case "provider-unknown": rule.Utility = (UtilityProvider)999; break;
            case "type": rule.AnomalyType = (AnomalyType)999; break;
            case "area": rule.AreaId = 999; break;
            case "priority-negative": rule.Priority = -1; break;
            case "priority-excess": rule.Priority = 10001; break;
            case "partial-bounds": rule.MinLatitude = 20; break;
            default:
                rule.MinLatitude = 20; rule.MaxLatitude = 30; rule.MinLongitude = 90; rule.MaxLongitude = 100;
                if (invalid == "latitude-range") rule.MinLatitude = -91;
                if (invalid == "longitude-range") rule.MaxLongitude = 181;
                if (invalid == "reversed-latitude") rule.MinLatitude = 31;
                if (invalid == "reversed-longitude") rule.MinLongitude = 101;
                break;
        }
        db.UtilityRoutingRules.Add(rule);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(20, await db.UtilityRoutingRules.CountAsync());
    }

    [Fact]
    public async Task NewRulesDefaultInactiveAndReferencedAreasCannotBeDeleted()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var rule = new UtilityRoutingRule { Name = "New unpublished rule", Utility = UtilityProvider.Desco, AreaId = 1 };
        db.UtilityRoutingRules.Add(rule); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.False((await db.UtilityRoutingRules.SingleAsync(r => r.Id == rule.Id)).IsActive);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM ReportAreas WHERE Id = 1;"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE UtilityRoutingRules SET IsActive = 2 WHERE Id = 1;"));
    }

    [Fact]
    public async Task MigrationSeedsOncePreservesReportsAndSupportsRollback()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261016221457_AddUtilityAnomalySchema";
        await migrator.MigrateAsync(previous);
        TestDb.AddUser(db);
        db.Reports.Add(new Report { UserId = 1, Title = "Existing anomaly", AnomalyType = AnomalyType.WaterLeak,
            Utility = UtilityProvider.Unassigned, ReferenceNo = "UA-000001", PhotoPaths = ["/uploads/reports/one.jpg"] });
        await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        Assert.Equal(20, await db.UtilityRoutingRules.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await db.UtilityRoutingRules.Where(r => r.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
        await migrator.MigrateAsync();
        Assert.Equal(20, await db.UtilityRoutingRules.CountAsync());
        Assert.False((await db.UtilityRoutingRules.AsNoTracking().SingleAsync(r => r.Id == 1)).IsActive);
        Assert.Equal(UtilityRoutingOutcome.NoMatch, (await new UtilityRoutingService(db).ResolveAsync(AnomalyType.WaterLeak)).Outcome);
        await migrator.MigrateAsync(previous);
        db.ChangeTracker.Clear();
        var report = await db.Reports.SingleAsync();
        Assert.Equal("Existing anomaly", report.Title); Assert.Equal("UA-000001", report.ReferenceNo);
        Assert.Equal(new[] { "/uploads/reports/one.jpg" }, report.PhotoPaths);
        await migrator.MigrateAsync();
        Assert.Equal(20, await db.UtilityRoutingRules.CountAsync());
        Assert.Equal(UtilityProvider.WaterAuthority, (await new UtilityRoutingService(db).ResolveAsync(AnomalyType.WaterLeak)).Utility);
        Assert.Single(await db.Reports.ToListAsync());
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }
}
