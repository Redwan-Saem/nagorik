using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record WeatherCollectionResult(int SucceededZones, int FailedZones, int SkippedZones);
public interface IWeatherCollector
{
    Task<WeatherCollectionResult> CollectAsync(CancellationToken ct = default);
}

public sealed class WeatherCollectionService(AppDbContext db, IWeatherService weather, TimeProvider clock,
    ILogger<WeatherCollectionService> logger) : IWeatherCollector
{
    public async Task<WeatherCollectionResult> CollectAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hour = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerHour, DateTimeKind.Utc);
        var zones = await db.Zones.AsNoTracking().OrderBy(z => z.Id).Select(z => new { z.Id, z.BoundaryGeoJson }).ToListAsync(ct);
        int succeeded = 0, failed = 0, skipped = 0;
        foreach (var zone in zones)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryLocation(zone.BoundaryGeoJson, out var latitude, out var longitude)) { skipped++; logger.LogWarning("Weather skipped invalid boundary for zone {ZoneId}", zone.Id); continue; }
            try
            {
                var result = await weather.FetchAsync(latitude, longitude, hour, ct);
                if (result.TimestampUtc != hour || result.TimestampUtc.Kind != DateTimeKind.Utc) throw new WeatherDataException("Weather window does not match the requested UTC hour.");
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                foreach (var (forecast, amounts) in new[] { (false, result.Past), (true, result.Forecast) })
                {
                    var row = await db.RainfallReadings.SingleOrDefaultAsync(r => r.ZoneId == zone.Id && r.TimestampUtc == hour &&
                        r.IsForecast == forecast && r.Source == OpenMeteoWeatherService.Source, ct);
                    if (row == null)
                    {
                        row = new RainfallReading { ZoneId = zone.Id, TimestampUtc = hour, IsForecast = forecast, Source = OpenMeteoWeatherService.Source };
                        db.RainfallReadings.Add(row);
                    }
                    row.FetchedAtUtc = clock.GetUtcNow().UtcDateTime;
                    row.Rainfall1HourMm = amounts.OneHourMm; row.Rainfall3HourMm = amounts.ThreeHoursMm; row.Rainfall6HourMm = amounts.SixHoursMm;
                }
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
                succeeded++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                // Preserve prior readings and their real timestamps, never insert fake zero rain.
                failed++; logger.LogWarning(error, "Weather collection failed for zone {ZoneId}; existing readings were retained", zone.Id);
            }
            finally { db.ChangeTracker.Clear(); }
        }
        return new(succeeded, failed, skipped);
    }

    // A representative bounding-box centre, not a surveyed station or polygon centroid.
    public static bool TryLocation(string geometryJson, out double latitude, out double longitude)
    {
        latitude = longitude = 0;
        try
        {
            using var json = JsonDocument.Parse(geometryJson);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                type.GetString() is not ("Polygon" or "MultiPolygon") || !root.TryGetProperty("coordinates", out var coordinates)) return false;
            double minLat = 90, maxLat = -90, minLon = 180, maxLon = -180; var count = 0;
            bool Visit(JsonElement node)
            {
                if (node.ValueKind != JsonValueKind.Array || node.GetArrayLength() == 0) return false;
                if (node[0].ValueKind == JsonValueKind.Number)
                {
                    if (node.GetArrayLength() != 2 || node[1].ValueKind != JsonValueKind.Number || !node[0].TryGetDouble(out var lon) || !node[1].TryGetDouble(out var lat) ||
                        !double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180) return false;
                    minLat = Math.Min(minLat, lat); maxLat = Math.Max(maxLat, lat); minLon = Math.Min(minLon, lon); maxLon = Math.Max(maxLon, lon); count++; return true;
                }
                return node.EnumerateArray().All(Visit);
            }
            if (!Visit(coordinates) || count < 4) return false;
            latitude = (minLat + maxLat) / 2; longitude = (minLon + maxLon) / 2; return true;
        }
        catch (JsonException) { return false; }
    }
}
