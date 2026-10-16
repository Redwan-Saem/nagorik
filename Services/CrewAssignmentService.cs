using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public record CrewDto(int Id, string Name, bool IsBusy);

public enum AssignOutcome
{
    Assigned,
    NeedsConfirmation,
    ReportNotFound,
    CrewNotFound,
    NotAssignable
}

public record AssignResult(AssignOutcome Outcome, string Message);

public class CrewAssignmentService
{
    private static readonly ReportStatus[] Active =
    {
        ReportStatus.Assigned,
        ReportStatus.InProgress
    };

    private readonly AppDbContext _db;
    private readonly ICrewTaskNotifier _notifier;
    private readonly ILogger<CrewAssignmentService> _log;
    private readonly ResidentReportStatusNotifier _residentNotifier;

    public CrewAssignmentService(
        AppDbContext db,
        ICrewTaskNotifier notifier,
        ILogger<CrewAssignmentService> log,
        ResidentReportStatusNotifier residentNotifier)
    {
        _db = db;
        _notifier = notifier;
        _log = log;
        _residentNotifier = residentNotifier;
    }

    public async Task<List<CrewDto>> GetCrewsAsync()
    {
        return await _db.Crews
            .OrderBy(c => c.Name)
            .Select(c => new CrewDto(
                c.Id,
                c.Name,
                _db.Reports.Any(r =>
                    r.AssignedCrewId == c.Id &&
                    Active.Contains(r.Status))))
            .ToListAsync();
    }

    public async Task<AssignResult> AssignAsync(
        int reportId,
        int crewId,
        int dispatcherId,
        bool confirmBusy)
    {
        var report = await _db.Reports
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null)
        {
            return new(
                AssignOutcome.ReportNotFound,
                "Report not found.");
        }

        var crew = await _db.Crews
            .FirstOrDefaultAsync(c => c.Id == crewId);

        if (crew == null)
        {
            return new(
                AssignOutcome.CrewNotFound,
                "Crew not found.");
        }

        if (report.Authority != Authority.CityCorporation ||
            report.Status != ReportStatus.Approved)
        {
            return new(
                AssignOutcome.NotAssignable,
                "This report cannot be assigned (already assigned, finished, or not a City Corporation issue).");
        }

        bool busy = await _db.Reports.AnyAsync(r =>
            r.AssignedCrewId == crewId &&
            Active.Contains(r.Status));

        if (busy && !confirmBusy)
        {
            return new(
                AssignOutcome.NeedsConfirmation,
                $"{crew.Name} is already working on an active task. Assign anyway?");
        }

        if (!ReportStatusWorkflowService.CanTransition(
                report.Status,
                ReportStatus.Assigned))
        {
            return new(
                AssignOutcome.NotAssignable,
                "Invalid report status transition.");
        }

        report.AssignedCrewId = crewId;
        report.Status = ReportStatus.Assigned;

        var changedAtUtc = DateTime.UtcNow;

        var history = new ReportStatusHistory
        {
            ReportId = report.Id,
            Status = ReportStatus.Assigned,
            ChangedAtUtc = changedAtUtc
        };
        _db.ReportStatusHistories.Add(history);

        var assignment = new CrewAssignment
        {
            ReportId = reportId,
            CrewId = crewId,
            DispatcherId = dispatcherId,
            AssignedAtUtc = changedAtUtc
        };
        _db.CrewAssignments.Add(assignment);

        await _db.SaveChangesAsync();

        await _residentNotifier.NotifyAsync(report);

        try
        {
            await _notifier.NotifyAssignedAsync(report, crew);
        }
        catch (Exception ex)
        {
            _log.LogWarning(
                ex,
                "Crew notification failed for report {ReportId} and crew {CrewId}",
                reportId,
                crewId);
        }

        return new(
            AssignOutcome.Assigned,
            $"Assigned to {crew.Name}.");
    }
}
