using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public class RiskComputationTests
{
    [Fact]
    public async Task UpdatesFreshZonesAndClearsStalePredictionsToUnknown()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var zone = RiskPredictionTests.Zone(); db.Zones.Add(zone); await db.SaveChangesAsync();
        var now = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
        foreach (var forecast in new[] { false, true }) db.RainfallReadings.Add(new() { ZoneId = zone.Id,
            TimestampUtc = now, FetchedAtUtc = now, IsForecast = forecast,
            Rainfall1HourMm = 20, Rainfall3HourMm = 60, Rainfall6HourMm = 120 });
        await db.SaveChangesAsync();
        var clock = new MutableClock(now);
        var job = new RiskComputationService(db, new(db, clock, Options.Create(new WeatherOptions())), new(),
            new(Options.Create(new RiskScoringOptions())), clock);
        Assert.Equal(1, await job.ComputeAsync());
        Assert.Equal(ZoneRiskLevel.High, zone.RiskLevel); Assert.Equal(now, zone.LastComputedAtUtc);
        Assert.Contains("Open-Meteo", zone.RiskFactorsJson);
        clock.Now = now.AddHours(2);
        await job.ComputeAsync();
        Assert.Equal(ZoneRiskLevel.Unknown, zone.RiskLevel);
        Assert.Contains("Fresh, aligned", zone.RiskFactorsJson);
    }

    [Fact]
    public async Task MissingVulnerabilityDoesNotBecomeLowRiskAndCancellationDoesNotPublish()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var zone = RiskPredictionTests.Zone(); zone.HistoryScore = null; db.Zones.Add(zone); await db.SaveChangesAsync();
        var clock = TimeProvider.System;
        var job = new RiskComputationService(db, new(db, clock, Options.Create(new WeatherOptions())), new(),
            new(Options.Create(new RiskScoringOptions())), clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.ComputeAsync(new CancellationToken(true)));
        Assert.Null(zone.LastComputedAtUtc);
        await job.ComputeAsync(); Assert.Equal(ZoneRiskLevel.Unknown, zone.RiskLevel);
        Assert.Contains("Missing vulnerability", zone.RiskFactorsJson);
    }
    private sealed class MutableClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
