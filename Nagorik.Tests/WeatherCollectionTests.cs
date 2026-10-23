using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public class WeatherCollectionTests
{
    public sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 22, 10, 15, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Provider : IWeatherService
    {
        public bool Fail; public bool InvalidForecast; public double Rain = 1; public Func<double, bool>? FailFor;
        public Task<WeatherRainfall> FetchAsync(double lat, double lon, DateTime hour, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Fail || FailFor?.Invoke(lat) == true) throw new HttpRequestException("Provider unavailable");
            Assert.InRange(lat, 23, 24); Assert.InRange(lon, 90, 91);
            return Task.FromResult(new WeatherRainfall(hour, new(Rain, Rain * 3, Rain * 6), InvalidForecast ? new(10, 1, 1) : new(Rain * 2, Rain * 6, Rain * 12)));
        }
    }
    [Fact]
    public async Task CollectionUpsertsSameHourAndFailurePreservesCacheWithoutFakeFreshness()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        db.Zones.Add(new Zone { Code = "Z1", Name = "Dhanmondi", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90.37,23.74],[90.38,23.74],[90.38,23.75],[90.37,23.74]]]}" });
        await db.SaveChangesAsync(); var clock = new Clock(); var provider = new Provider();
        var collector = new WeatherCollectionService(db, provider, clock, NullLogger<WeatherCollectionService>.Instance);
        Assert.Equal(1, (await collector.CollectAsync()).SucceededZones);
        provider.Rain = 2; clock.Now = clock.Now.AddMinutes(5);
        await collector.CollectAsync(); Assert.Equal(2, await db.RainfallReadings.CountAsync());
        var past = await db.RainfallReadings.AsNoTracking().SingleAsync(r => !r.IsForecast);
        Assert.Equal(12, past.Rainfall6HourMm); Assert.Equal(clock.Now.UtcDateTime, past.FetchedAtUtc);
        provider.Fail = true; clock.Now = clock.Now.AddHours(3);
        Assert.Equal(1, (await collector.CollectAsync()).FailedZones);
        var preserved = await db.RainfallReadings.AsNoTracking().SingleAsync(r => !r.IsForecast);
        Assert.Equal(past.FetchedAtUtc, preserved.FetchedAtUtc); Assert.Equal(past.Rainfall6HourMm, preserved.Rainfall6HourMm);
        Assert.Equal(2, await db.RainfallReadings.CountAsync());
    }

    [Fact]
    public async Task InvalidBoundaryIsSkippedAndCancellationStopsCollection()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        db.Zones.Add(new Zone { Code = "BAD", Name = "Bad geometry", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[]]}" }); await db.SaveChangesAsync();
        var collector = new WeatherCollectionService(db, new Provider(), new Clock(), NullLogger<WeatherCollectionService>.Instance);
        Assert.Equal(1, (await collector.CollectAsync()).SkippedZones); Assert.Empty(await db.RainfallReadings.ToListAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collector.CollectAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task FailedZoneDoesNotStopAnotherZoneAndInvalidPairRollsBack()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        db.Zones.AddRange(new Zone { Code = "Z1", Name = "First", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90.37,23.74],[90.38,23.74],[90.38,23.75],[90.37,23.74]]]}" },
            new Zone { Code = "Z2", Name = "Second", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90.37,23.8],[90.38,23.8],[90.38,23.9],[90.37,23.8]]]}" });
        await db.SaveChangesAsync();
        var provider = new Provider { FailFor = lat => lat < 23.8 };
        var collector = new WeatherCollectionService(db, provider, new Clock(), NullLogger<WeatherCollectionService>.Instance);
        var result = await collector.CollectAsync(); Assert.Equal(1, result.SucceededZones); Assert.Equal(1, result.FailedZones);
        Assert.Equal(2, await db.RainfallReadings.CountAsync());
        await db.RainfallReadings.ExecuteDeleteAsync(); provider.FailFor = null; provider.InvalidForecast = true;
        Assert.Equal(2, (await collector.CollectAsync()).FailedZones);
        Assert.Empty(await db.RainfallReadings.ToListAsync());
    }

    private sealed class Probe { public int Calls; public TaskCompletionSource First = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private sealed class FakeCollector(Probe probe, bool fail) : IWeatherCollector
    {
        public Task<WeatherCollectionResult> CollectAsync(CancellationToken ct = default)
        {
            probe.Calls++; probe.First.TrySetResult();
            return fail ? Task.FromException<WeatherCollectionResult>(new HttpRequestException("Offline")) : Task.FromResult(new WeatherCollectionResult(1, 0, 0));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerRunsImmediatelySurvivesFailureAndStopsDuringScheduledWait(bool fail)
    {
        var probe = new Probe(); var services = new ServiceCollection(); services.AddScoped<IWeatherCollector>(_ => new FakeCollector(probe, fail));
        await using var provider = services.BuildServiceProvider();
        using var worker = new WeatherBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new WeatherOptions()), TimeProvider.System, NullLogger<WeatherBackgroundService>.Instance);
        await worker.StartAsync(CancellationToken.None); await probe.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task DisabledWorkerMakesNoCollections()
    {
        var probe = new Probe(); var services = new ServiceCollection(); services.AddScoped<IWeatherCollector>(_ => new FakeCollector(probe, false));
        await using var provider = services.BuildServiceProvider();
        using var worker = new WeatherBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new WeatherOptions { Enabled = false }), TimeProvider.System, NullLogger<WeatherBackgroundService>.Instance);
        await worker.StartAsync(CancellationToken.None); await worker.StopAsync(CancellationToken.None);
        Assert.Equal(0, probe.Calls);
    }
}
