namespace Nagorik.Api.Models;

public enum SosAlertEventType
{
    Triggered,
    LocationUpdated,
    ResponderAssigned,
    Completed,
    Cancelled
}

// An immutable snapshot of the alert at the time of an action, not a live location reference.
public class SosAlertEvent
{
    public int Id { get; set; }
    public int SosAlertId { get; set; }
    public SosAlert? SosAlert { get; set; }
    public SosAlertEventType EventType { get; set; }
    public SosAlertStatus Status { get; set; }
    public int ActorUserId { get; set; }
    public User? ActorUser { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string AddressText { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
