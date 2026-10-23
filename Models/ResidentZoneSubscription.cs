namespace Nagorik.Api.Models;
public sealed class ResidentZoneSubscription
{
    public long Id { get; set; }
    public int ResidentId { get; set; }
    public User Resident { get; set; } = null!;
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public bool AlertsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
