namespace Nagorik.Api.Models;
public enum RiskDeliveryState { Pending, Sending, Sent, Failed, Suppressed }
public sealed class RiskAlertDelivery
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public RiskAlertEvent Event { get; set; } = null!;
    public int ResidentId { get; set; }
    public User Resident { get; set; } = null!;
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public RiskDeliveryState State { get; set; } = RiskDeliveryState.Pending;
    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public string? ClaimToken { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}
public sealed class RiskAlertProcessorLease
{
    public int Id { get; set; } = 1;
    public string Token { get; set; } = "";
    public DateTime LockedUntilUtc { get; set; }
}
