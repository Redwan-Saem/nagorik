namespace Nagorik.Api.Models;

public enum DrainageStatus { Unknown, Clear, Blocked }

public sealed class DrainagePoint
{
    public int Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DrainageStatus Status { get; set; } = DrainageStatus.Unknown;
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public int? LastWorkReportId { get; set; }
    public Report? LastWorkReport { get; set; }
}
