using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public class WeatherReadingQueryTests
{
    [Fact]
    public async Task LatestPairIsAvailableThenStaleWithoutChangingCachedTimes()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var zone = new Zone { Code = "Z1", Name = "Zone", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" };
        db.Zones.Add(zone); await db.SaveChangesAsync(); var clock = new WeatherCollectionTests.Clock();
        var options = Options.Create(new WeatherOptions { MaxAgeMinutes = 90 });
        var query = new WeatherReadingQueryService(db, clock, options);
        Assert.Equal("Unavailable", (await query.ListAsync()).Zones[0].State);
        var hour = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
        foreach (var forecast in new[] { false, true })
            db.RainfallReadings.Add(new RainfallReading { ZoneId = zone.Id, TimestampUtc = hour, FetchedAtUtc = clock.Now.UtcDateTime, IsForecast = forecast, Rainfall1HourMm = 1, Rainfall3HourMm = 3, Rainfall6HourMm = 6 });
        db.RainfallReadings.Add(new RainfallReading { ZoneId = zone.Id, TimestampUtc = hour.AddHours(-1), FetchedAtUtc = clock.Now.UtcDateTime, Rainfall1HourMm = 2, Rainfall3HourMm = 6, Rainfall6HourMm = 12 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var fresh = (await query.ListAsync()).Zones[0]; Assert.Equal("Available", fresh.State); Assert.Equal(hour, fresh.Past!.TimestampUtc);
        Assert.Equal(6, fresh.Past.Rainfall6HourMm); Assert.False(fresh.Past.IsStale); Assert.Equal(15, fresh.Past.AgeMinutes);
        clock.Now = clock.Now.AddHours(3);
        var stale = (await query.ListAsync()).Zones[0]; Assert.Equal("Stale", stale.State); Assert.True(stale.Forecast!.IsStale);
        Assert.Equal(fresh.Past.FetchedAtUtc, stale.Past!.FetchedAtUtc); Assert.Equal(6, stale.Past.Rainfall6HourMm);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
