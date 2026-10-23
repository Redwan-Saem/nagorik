using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record RiskComputationDetails(RiskAssessment Assessment, double? ElevationMeters,
    double? DrainageDensityKmPerSqKm, double? HistoryScore, WeatherSnapshot? Past,
    WeatherSnapshot? Forecast, RiskScoringOptions Thresholds, int GeoRevision=0,string? InputSource=null,bool? InputIsSample=null);

public class RiskThresholdProvider(IOptions<RiskScoringOptions> defaults)
{
    public virtual Task<RiskScoringOptions> GetAsync(CancellationToken ct = default) => Task.FromResult(defaults.Value);
}

public sealed class RiskComputationService(AppDbContext db, WeatherReadingQueryService weather,
    RiskPredictionService predictor, RiskThresholdProvider thresholds, TimeProvider clock)
{
    public async Task<int> ComputeAsync(CancellationToken ct = default)
    {
        var settings = await thresholds.GetAsync(ct);
        var snapshots = (await weather.ListAsync(ct)).Zones.ToDictionary(z => z.ZoneId);
        // One transaction publishes all zone levels/explanations together.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var zones = await db.Zones.OrderBy(z => z.Id).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var zone in zones)
        {
            ct.ThrowIfCancellationRequested();
            snapshots.TryGetValue(zone.Id, out var snapshot);
            var result = predictor.Predict(zone, snapshot, settings);
            var previousLevel = zone.RiskLevel;
            zone.RiskLevel = result.Level;
            zone.LastComputedAtUtc = now;
            zone.RiskFactorsJson = JsonSerializer.Serialize(new RiskComputationDetails(result, zone.ElevationMeters,
                zone.DrainageDensityKmPerSqKm, zone.HistoryScore, snapshot?.Past, snapshot?.Forecast, settings,zone.GeoRevision,zone.InputSource,zone.InputIsSample));
            var history = new RiskHistory { ZoneId = zone.Id, ComputedAtUtc = now,
                PreviousLevel = previousLevel, Level = result.Level, FactorsJson = zone.RiskFactorsJson };
            db.RiskHistories.Add(history);
            if (previousLevel != result.Level && result.Level is ZoneRiskLevel.High or ZoneRiskLevel.Severe)
                db.RiskAlertEvents.Add(new RiskAlertEvent { RiskHistory = history, CreatedAtUtc = now });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return zones.Count;
    }
}
