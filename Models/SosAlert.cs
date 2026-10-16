namespace Nagorik.Api.Models;

public enum SosAlertStatus
{
    Active,
    Responding,
    Completed,
    Cancelled
}

public class SosAlert
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string AddressText { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public SosAlertStatus Status { get; set; } = SosAlertStatus.Active;
    public int? ResponderUserId { get; set; }
    public User? ResponderUser { get; set; }
}
