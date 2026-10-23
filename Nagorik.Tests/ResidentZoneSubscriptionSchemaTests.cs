using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Tests;
public class ResidentZoneSubscriptionSchemaTests
{
    [Fact]
    public async Task SubscriptionIsUniqueAndReferencesRealResidentAndZone()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var resident = new User { Role = "Resident" }; var zone = RiskPredictionTests.Zone();
        db.ResidentZoneSubscriptions.Add(new() { Resident = resident, Zone = zone, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(); db.ResidentZoneSubscriptions.Add(new() { ResidentId = resident.Id, ZoneId = zone.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.ResidentZoneSubscriptions.Add(new() { ResidentId = 999, ZoneId = zone.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var saved = await db.ResidentZoneSubscriptions.SingleAsync(); Assert.True(saved.AlertsEnabled);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM Zones"));
    }
}
