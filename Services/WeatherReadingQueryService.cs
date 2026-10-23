using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record WeatherSnapshot(DateTime TimestampUtc, DateTime FetchedAtUtc, string Source,
    double Rainfall1HourMm, double Rainfall3HourMm, double Rainfall6HourMm, double AgeMinutes, bool IsStale);
public sealed record ZoneWeatherStatus(int ZoneId, string Code, string Name, string State, WeatherSnapshot? Past, WeatherSnapshot? Forecast);
public sealed record WeatherMapStatus(bool CollectionEnabled, string Definition, IReadOnlyList<ZoneWeatherStatus> Zones);

public sealed class WeatherReadingQueryService(AppDbContext db, TimeProvider clock, IOptions<WeatherOptions> options)
{
    public async Task<WeatherMapStatus> ListAsync(CancellationToken ct = default)
    {
        var zones = await db.Zones.AsNoTracking().OrderBy(z => z.Code).Select(z => new { z.Id, z.Code, z.Name }).ToListAsync(ct);
        // Select latest per zone/type in SQL, not the entire accumulated history.
        var latest = await db.RainfallReadings.AsNoTracking().Where(r => r.Source == OpenMeteoWeatherService.Source)
            .GroupBy(r => new { r.ZoneId, r.IsForecast }).Select(g => g.OrderByDescending(r => r.TimestampUtc).ThenByDescending(r => r.FetchedAtUtc).First()).ToListAsync(ct);
        var lookup = latest.ToDictionary(r => (r.ZoneId, r.IsForecast));
        var now = clock.GetUtcNow().UtcDateTime;
        WeatherSnapshot? Snapshot(int zoneId, bool forecast)
        {
            if (!lookup.TryGetValue((zoneId, forecast), out var row)) return null;
            var timestamp = DateTime.SpecifyKind(row.TimestampUtc, DateTimeKind.Utc);
            var fetched = DateTime.SpecifyKind(row.FetchedAtUtc, DateTimeKind.Utc);
            var age = Math.Max(0, Math.Max((now - timestamp).TotalMinutes, (now - fetched).TotalMinutes));
            return new(timestamp, fetched, row.Source, row.Rainfall1HourMm, row.Rainfall3HourMm, row.Rainfall6HourMm,
                Math.Round(age, 2), age > options.Value.MaxAgeMinutes || timestamp > now || fetched > now.AddMinutes(1));
        }
        var result = zones.Select(z =>
        {
            var past = Snapshot(z.Id, false); var forecast = Snapshot(z.Id, true);
            var state = past == null && forecast == null ? "Unavailable" : past?.IsStale == true || forecast?.IsStale == true ? "Stale" :
                past == null || forecast == null ? "Partial" : "Available";
            return new ZoneWeatherStatus(z.Id, z.Code, z.Name, state, past, forecast);
        }).ToList();
        return new(options.Value.Enabled, "Millimetre accumulations: past windows end at TimestampUtc; forecast windows start there. Open-Meteo model data, not ground-station observations.", result);
    }
}
