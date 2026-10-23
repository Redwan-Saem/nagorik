using System.ComponentModel.DataAnnotations;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public class RiskPredictionTests
{
    public static Zone Zone() => new() { Code = "R", Name = "Risk zone", BoundaryGeoJson =
        "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}",
        ElevationMeters = 10, DrainageDensityKmPerSqKm = 5, HistoryScore = 0 };
    public static ZoneWeatherStatus Weather(double rain, bool stale = false)
    {
        var snapshot = new WeatherSnapshot(DateTime.UtcNow.Date, DateTime.UtcNow, "Open-Meteo", rain, rain * 3, rain * 6, 0, stale);
        return new(1, "R", "Risk zone", "Available", snapshot, snapshot);
    }
    [Theory]
    [InlineData(0, ZoneRiskLevel.Low)]
    [InlineData(4.99, ZoneRiskLevel.Low)]
    [InlineData(5, ZoneRiskLevel.Medium)]
    [InlineData(15, ZoneRiskLevel.High)]
    [InlineData(30, ZoneRiskLevel.Severe)]
    public void ThresholdBoundariesAndConsistentHourlyUnits(double rainfall, ZoneRiskLevel expected)
    {
        var result = new RiskPredictionService().Predict(Zone(), Weather(rainfall), new());
        Assert.Equal(expected, result.Level); Assert.Equal(rainfall, result.Score);
    }
    [Fact]
    public void VulnerabilityAndForecastIncreaseRisk()
    {
        var zone = Zone(); zone.ElevationMeters = 0; zone.DrainageDensityKmPerSqKm = 0; zone.HistoryScore = 1;
        var weather = Weather(1); weather = weather with { Forecast = weather.Forecast! with { Rainfall1HourMm = 10, Rainfall3HourMm = 30, Rainfall6HourMm = 60 } };
        var result = new RiskPredictionService().Predict(zone, weather, new());
        Assert.Equal(4, result.Vulnerability); Assert.Equal(40, result.Score); Assert.Equal(ZoneRiskLevel.Severe, result.Level);
    }
    [Fact]
    public void MissingStaleOrUnalignedInputsProduceUnknown()
    {
        var engine = new RiskPredictionService(); var zone = Zone();
        Assert.Equal(ZoneRiskLevel.Unknown, engine.Predict(zone, Weather(10, true), new()).Level);
        Assert.Equal(ZoneRiskLevel.Unknown, engine.Predict(zone, null, new()).Level);
        var weather = Weather(1); weather = weather with { Forecast = weather.Forecast! with { TimestampUtc = DateTime.MinValue } };
        Assert.Equal(ZoneRiskLevel.Unknown, engine.Predict(zone, weather, new()).Level);
        zone.ElevationMeters = null;
        Assert.Null(engine.Predict(zone, Weather(10), new()).Score);
    }
    [Fact]
    public void InvalidThresholdsFailInsteadOfMisclassifying()
    {
        Assert.Throws<ValidationException>(() => new RiskPredictionService().Predict(Zone(), Weather(10), new() { HighThreshold = 1 }));
    }
}
