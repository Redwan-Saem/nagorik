using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public class CrewTaskNotifier : ICrewTaskNotifier
{
    public const string Title = "New task assigned";
    public const int MaxBodyLength = 140;

    private readonly INotificationService _notifications;
    private readonly ILogger<CrewTaskNotifier> _log;

    public CrewTaskNotifier(
        INotificationService notifications,
        ILogger<CrewTaskNotifier> log)
    {
        _notifications = notifications;
        _log = log;
    }

    public async Task NotifyAssignedAsync(Report report, Crew crew)
    {
        var delivered = await _notifications.SendToUserAsync(
            crew.UserId.ToString(),
            Title,
            BuildBody(report),
            BuildUrl(report));

        if (delivered == 0)
        {
            _log.LogWarning(
                "Crew {CrewId} (user {UserId}) has no device that received the task notification for report {ReportId}",
                crew.Id, crew.UserId, report.Id);
        }
        else
        {
            _log.LogInformation(
                "Task notification for report {ReportId} delivered to {Delivered} device(s) of crew {CrewId}",
                report.Id, delivered, crew.Id);
        }
    }

    public static string BuildUrl(Report report) => $"/crew/tasks/{report.Id}";

    public static string BuildBody(Report report)
    {
        var code = $"R-{report.Id:D6}";

        var location = string.IsNullOrWhiteSpace(report.Location)
            ? "location not provided"
            : report.Location.Trim();

        var body = $"{code} - {report.Category} - {location}";

        return body.Length <= MaxBodyLength
            ? body
            : body[..(MaxBodyLength - 3)] + "...";
    }
}
