using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
using Nagorik.Api.Models;

namespace Nagorik.Tests;

public class RainfallSchemaTests
{
    private static RainfallReading Reading() => new() { Zone = new Zone { Code = "Z1", Name = "Zone",
        BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" },
        TimestampUtc = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc), FetchedAtUtc = DateTime.UtcNow,
        Rainfall1HourMm = 1, Rainfall3HourMm = 3, Rainfall6HourMm = 6 };

    [Fact]
    public async Task MigratesStoresPastAndForecastAndRollbackPreservesZone()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync(); var reading = Reading(); db.RainfallReadings.Add(reading); await db.SaveChangesAsync();
        db.RainfallReadings.Add(new RainfallReading { ZoneId = reading.ZoneId, TimestampUtc = reading.TimestampUtc,
            FetchedAtUtc = reading.FetchedAtUtc, IsForecast = true, Rainfall1HourMm = 2, Rainfall3HourMm = 4, Rainfall6HourMm = 8 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.RainfallReadings.OrderBy(r => r.IsForecast).ToListAsync();
        Assert.Equal(2, saved.Count); Assert.Equal(6, saved[0].Rainfall6HourMm); Assert.Equal(8, saved[1].Rainfall6HourMm);
        Assert.Equal(reading.TimestampUtc, saved[0].TimestampUtc);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM Zones"));
        await db.GetService<IMigrator>().MigrateAsync("20261022135042_AddDrainagePoints");
        Assert.Single(await db.Zones.ToListAsync()); await db.Database.MigrateAsync(); Assert.Empty(await db.RainfallReadings.ToListAsync());
    }

    [Fact]
    public async Task SameZoneHourForecastAndSourceCannotDuplicate()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var first = Reading(); db.RainfallReadings.Add(first); await db.SaveChangesAsync();
        db.RainfallReadings.Add(new RainfallReading { ZoneId = first.ZoneId, TimestampUtc = first.TimestampUtc, FetchedAtUtc = first.FetchedAtUtc });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(-1, 3, 6)]
    [InlineData(4, 3, 6)]
    [InlineData(1, 7, 6)]
    [InlineData(1, 3, 10001)]
    [InlineData(1, 3, double.PositiveInfinity)]
    public async Task InvalidAccumulationsAreRejected(double one, double three, double six)
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var value = Reading(); value.Rainfall1HourMm = one; value.Rainfall3HourMm = three; value.Rainfall6HourMm = six;
        db.RainfallReadings.Add(value); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
