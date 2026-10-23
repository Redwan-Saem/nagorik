using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
namespace Nagorik.Tests;
public class NonMlMigrationTests
{
    [Fact]public async Task UpgradingFromStory30PreservesCrewAndLegacyOptInAndRollbackIsReversible()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        const string previous="20261022194234_AddRiskThresholdSettings";
        await db.GetService<IMigrator>().MigrateAsync(previous);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Users (Id,Name,Email,PhoneNumber,PasswordHash,Role) VALUES (1,'Existing','old@test.invalid','','fixture','Resident')");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Crews (Name,UserId) VALUES ('Existing crew',1)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO AreaSubscriptions (UserId,ZoneId) VALUES ('1','Z1')");
        await db.Database.MigrateAsync();
        var crew=await db.Crews.SingleAsync();Assert.Equal("Existing crew",crew.Name);Assert.Null(crew.LastLatitude);Assert.Null(crew.LastSeenAtUtc);
        Assert.Single(await db.AreaSubscriptions.ToListAsync());Assert.Empty(await db.ResidentZoneSubscriptions.ToListAsync());
        Assert.Empty(await db.RiskAlertDeliveries.ToListAsync());db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync(previous);
        Assert.Equal("Existing crew",await db.Crews.Select(c=>c.Name).SingleAsync());
        Assert.Single(await db.AreaSubscriptions.ToListAsync());await db.Database.MigrateAsync();Assert.Single(await db.Crews.ToListAsync());
    }
}
