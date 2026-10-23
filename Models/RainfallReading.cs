namespace Nagorik.Api.Models;

public sealed class RainfallReading
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;

    // UTC hour boundary: past windows end here, forecast windows start here.
    public DateTime TimestampUtc { get; set; }
    public DateTime FetchedAtUtc { get; set; }
    public bool IsForecast { get; set; }
    public string Source { get; set; } = "Open-Meteo";

    // Accumulated rain in millimetres, not average intensity or a risk score.
    public double Rainfall1HourMm { get; set; }
    public double Rainfall3HourMm { get; set; }
    public double Rainfall6HourMm { get; set; }
}
