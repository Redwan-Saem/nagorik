namespace Nagorik.Api.Models;

public sealed class RiskHistory
{
    public long Id { get; set; }
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public DateTime ComputedAtUtc { get; set; }
    public ZoneRiskLevel PreviousLevel { get; set; }
    public ZoneRiskLevel Level { get; set; }
    public string RuleVersion { get; set; } = "rule-v1";
    public string FactorsJson { get; set; } = "";
}
