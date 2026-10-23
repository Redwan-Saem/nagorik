using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
using Nagorik.Api.Models;

namespace Nagorik.Tests;

public class RiskHistorySchemaTests
{
    [Fact]
    public async Task MigratesAuditAndRollsBackWithoutLosingZoneOrVulnerability()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var zone = RiskPredictionTests.Zone(); db.Zones.Add(zone); await db.SaveChangesAsync();
        db.RiskHistories.Add(new() { ZoneId = zone.Id, ComputedAtUtc = DateTime.UtcNow, FactorsJson = "{\"score\":12}" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal("rule-v1", (await db.RiskHistories.SingleAsync()).RuleVersion);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM Zones"));
        await db.GetService<IMigrator>().MigrateAsync("20261022192830_AddZoneVulnerability");
        Assert.Equal(10, (await db.Zones.Select(z => z.ElevationMeters).SingleAsync()));
        await db.Database.MigrateAsync(); Assert.Empty(await db.RiskHistories.ToListAsync());
    }
}
