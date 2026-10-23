namespace Nagorik.Api.Models;
public sealed class RiskAlertEvent
{
    public long Id { get; set; }
    public long RiskHistoryId { get; set; }
    public RiskHistory RiskHistory { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
