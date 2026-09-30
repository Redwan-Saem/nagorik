namespace Nagorik.Api.Models;

public class UserPushSubscription
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";

    public string Endpoint { get; set; } = "";

    public string P256dh { get; set; } = "";

    public string Auth { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}