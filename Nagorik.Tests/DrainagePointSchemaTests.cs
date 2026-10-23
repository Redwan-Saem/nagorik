using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
using Nagorik.Api.Models;

namespace Nagorik.Tests;

public class DrainagePointSchemaTests
{
    private static Zone Zone() => new() { Code = "TEST", Name = "Test zone", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" };

    [Fact]
    public async Task MigrationStoresPointAndZoneAndRestrictsZoneDeletion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var zone = Zone();
        db.DrainagePoints.Add(new DrainagePoint { Latitude = 23.74, Longitude = 90.37, Zone = zone });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.DrainagePoints.Include(p => p.Zone).SingleAsync();
        Assert.Equal(DrainageStatus.Unknown, saved.Status);
        Assert.Equal("TEST", saved.Zone.Code);
        Assert.Equal(23.74, saved.Latitude); Assert.Equal(90.37, saved.Longitude);
        saved.Status = DrainageStatus.Blocked; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(DrainageStatus.Blocked, (await db.DrainagePoints.SingleAsync()).Status);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM Zones"));
        await db.GetService<IMigrator>().MigrateAsync("20261022125900_AddZones");
        // Query only columns present in the historical migration being tested.
        Assert.Equal("TEST", await db.Zones.Select(z => z.Code).SingleAsync());
        await db.Database.MigrateAsync();
        Assert.Empty(await db.DrainagePoints.ToListAsync());
    }

    [Theory]
    [InlineData(91, 90, DrainageStatus.Clear, false)]
    [InlineData(-91, 90, DrainageStatus.Clear, false)]
    [InlineData(23, 181, DrainageStatus.Clear, false)]
    [InlineData(23, -181, DrainageStatus.Clear, false)]
    [InlineData(23, 90, (DrainageStatus)999, false)]
    [InlineData(23, 90, DrainageStatus.Clear, true)]
    public async Task ConstraintsRejectInvalidCoordinatesStatusAndMissingZone(double latitude, double longitude, DrainageStatus status, bool missingZone)
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        var point = new DrainagePoint { Latitude = latitude, Longitude = longitude, Status = status };
        if (missingZone) point.ZoneId = 999; else point.Zone = Zone();
        db.DrainagePoints.Add(point);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public async Task CoordinateEdgesAreAccepted(double latitude, double longitude)
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        db.DrainagePoints.Add(new DrainagePoint { Latitude = latitude, Longitude = longitude, Status = DrainageStatus.Clear, Zone = Zone() });
        await db.SaveChangesAsync(); Assert.Single(await db.DrainagePoints.ToListAsync());
    }
}
