namespace Nagorik.Api.Models;

public sealed class RiskThresholdSettings
{
    public int Id { get; set; } = 1;
    public double MediumThreshold { get; set; }
    public double HighThreshold { get; set; }
    public double SevereThreshold { get; set; }
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string UpdatedByUserId { get; set; } = "";
    public RiskScoringOptions ToOptions() => new() { MediumThreshold = MediumThreshold, HighThreshold = HighThreshold, SevereThreshold = SevereThreshold };
}
