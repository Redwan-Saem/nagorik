using System.ComponentModel.DataAnnotations;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record RiskAssessment(ZoneRiskLevel Level, double? Score, double? IntensityMmPerHour,
    double? Vulnerability, string Explanation);

// Transparent baseline, not a calibrated flood forecast or emergency instruction.
public sealed class RiskPredictionService
{
    public RiskAssessment Predict(Zone zone, ZoneWeatherStatus? weather, RiskScoringOptions thresholds)
    {
        Validator.ValidateObject(thresholds, new ValidationContext(thresholds), true);
        if (zone.ElevationMeters is not { } elevation || zone.DrainageDensityKmPerSqKm is not { } density || zone.HistoryScore is not { } history)
            return Unknown("Missing vulnerability data; enter elevation, drainage density and historical score.");
        if (!double.IsFinite(elevation) || elevation is < -500 or > 9000 || !double.IsFinite(density) || density is < 0 or > 100 ||
            !double.IsFinite(history) || history is < 0 or > 1) return Unknown("Invalid vulnerability data.");
        if (weather?.Past is not { IsStale: false } past || weather.Forecast is not { IsStale: false } forecast ||
            past.TimestampUtc != forecast.TimestampUtc) return Unknown("Fresh, aligned past and forecast rainfall windows are required.");
        var windows = new[] { past, forecast };
        if (windows.Any(w => !Valid(w))) return Unknown("Invalid rainfall accumulations.");
        // Compare like units: 3h and 6h accumulations become mean mm/hour.
        var intensity = windows.Max(w => Math.Max(w.Rainfall1HourMm, Math.Max(w.Rainfall3HourMm / 3, w.Rainfall6HourMm / 6)));
        var vulnerability = 1 + Math.Clamp((10 - elevation) / 10, 0, 1) + Math.Clamp((5 - density) / 5, 0, 1) + history;
        var score = intensity * vulnerability;
        var level = score >= thresholds.SevereThreshold ? ZoneRiskLevel.Severe : score >= thresholds.HighThreshold ? ZoneRiskLevel.High :
            score >= thresholds.MediumThreshold ? ZoneRiskLevel.Medium : ZoneRiskLevel.Low;
        return new(level, score, intensity, vulnerability, "Rule baseline: maximum past/forecast 1h, 3h/3, 6h/6 rainfall × (1 + low-elevation factor + drainage-deficit factor + history score). Open-Meteo model data; not a validated flood forecast.");
    }

    private static RiskAssessment Unknown(string reason) => new(ZoneRiskLevel.Unknown, null, null, null, reason);
    private static bool Valid(WeatherSnapshot w) => double.IsFinite(w.Rainfall1HourMm) && double.IsFinite(w.Rainfall3HourMm) &&
        double.IsFinite(w.Rainfall6HourMm) && w.Rainfall1HourMm >= 0 && w.Rainfall3HourMm >= w.Rainfall1HourMm &&
        w.Rainfall6HourMm >= w.Rainfall3HourMm && w.Rainfall6HourMm <= 10000;
}
