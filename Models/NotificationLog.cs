namespace Nagorik.Api.Models;

public class NotificationLog
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public string Url { get; set; } = "";

    public bool Success { get; set; }

    public DateTime? PredictionTimestampUtc { get; set; }

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}