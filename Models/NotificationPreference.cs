namespace Nagorik.Api.Models;

public class NotificationPreference
{
    public string UserId { get; set; } = "";

    public bool NotificationsEnabled { get; set; }
}