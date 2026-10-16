using System.Globalization;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public class ResidentReportStatusNotifier(
    INotificationService notifications,
    ILogger<ResidentReportStatusNotifier> log)
{
    // Call only after the report and its history have been committed.
    public async Task NotifyAsync(Report report)
    {
        try
        {
            await notifications.SendToUserAsync(
                report.UserId.ToString(CultureInfo.InvariantCulture),
                "Report status updated",
                $"R-{report.Id:D6} status changed to {report.Status}.",
                $"/Reports/Detail/{report.Id}");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "Resident notification failed for report {ReportId} and user {UserId}",
                report.Id, report.UserId);
        }
    }
}
