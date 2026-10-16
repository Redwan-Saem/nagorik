using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public class DispatcherTaskStatusNotifier(
    AppDbContext db,
    INotificationService notifications,
    ILogger<DispatcherTaskStatusNotifier> log)
{
    public async Task NotifyAsync(Report report, int crewUserId)
    {
        try
        {
            var assignment = await db.CrewAssignments.AsNoTracking()
                .Where(a => a.ReportId == report.Id)
                .OrderByDescending(a => a.AssignedAtUtc).ThenByDescending(a => a.Id)
                .Select(a => new { a.DispatcherId, a.CrewId, a.Crew!.UserId, a.Crew!.Name })
                .FirstOrDefaultAsync();

            if (assignment == null || assignment.CrewId != report.AssignedCrewId || assignment.UserId != crewUserId)
            {
                log.LogWarning("No matching assignment dispatcher found for report {ReportId}", report.Id);
                return;
            }

            await notifications.SendToUserAsync(
                assignment.DispatcherId.ToString(CultureInfo.InvariantCulture),
                "Crew task status updated",
                $"R-{report.Id:D6} - {assignment.Name} (crew #{assignment.CrewId}) - {report.Status}.",
                $"/Dispatch/Report/{report.Id}");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Dispatcher notification failed for report {ReportId}", report.Id);
        }
    }
}
