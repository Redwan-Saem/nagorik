using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public enum StatusChangeOutcome
{
    Changed,
    ReportNotFound,
    InvalidTransition,
    Conflict
}

public record StatusChangeResult(
    StatusChangeOutcome Outcome,
    string Message);

public class ReportStatusWorkflowService
{
    private readonly AppDbContext _db;
    private readonly ResidentReportStatusNotifier _residentNotifier;
    private readonly DispatcherTaskStatusNotifier _dispatcherNotifier;

    public ReportStatusWorkflowService(AppDbContext db, ResidentReportStatusNotifier residentNotifier,
        DispatcherTaskStatusNotifier dispatcherNotifier)
    {
        _db = db;
        _residentNotifier = residentNotifier;
        _dispatcherNotifier = dispatcherNotifier;
    }

    public static bool CanTransition(
        ReportStatus current,
        ReportStatus next)
    {
        return current switch
        {
            ReportStatus.Pending =>
                next is ReportStatus.Approved
                    or ReportStatus.Rejected,

            ReportStatus.Approved =>
                next is ReportStatus.Assigned
                    or ReportStatus.Rejected,

            ReportStatus.Assigned =>
                next == ReportStatus.InProgress,

            ReportStatus.InProgress =>
                next == ReportStatus.Completed,

            _ => false
        };
    }

    public async Task<StatusChangeResult> ChangeStatusAsync(
        int reportId,
        ReportStatus nextStatus,
        int actorUserId)
    {
        var report = await _db.Reports
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null)
        {
            return new(
                StatusChangeOutcome.ReportNotFound,
                "Report not found.");
        }

        return await ApplyChangeAsync(report, nextStatus, actorUserId);
    }

    public async Task<StatusChangeResult> ChangeCrewTaskStatusAsync(
        int reportId,
        ReportStatus nextStatus,
        int crewUserId,
        CancellationToken ct = default, CrewLocationUpdate? location = null)
    {
        var report = await _db.Reports.Include(r => r.AssignedCrew).FirstOrDefaultAsync(r =>
            r.Id == reportId && r.AssignedCrew != null &&
            r.AssignedCrew.UserId == crewUserId, ct);

        if (report == null)
            return new(StatusChangeOutcome.ReportNotFound, "Task not found.");

        if (nextStatus is not (ReportStatus.InProgress or ReportStatus.Completed))
            return new(StatusChangeOutcome.InvalidTransition, "Crew may only start or complete a task.");

        if (location != null)
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(location, new(location), true);
        var result = await ApplyChangeAsync(report, nextStatus, crewUserId, ct, location);
        if (result.Outcome == StatusChangeOutcome.Changed)
            await _dispatcherNotifier.NotifyAsync(report, crewUserId);
        return result;
    }

    private async Task<StatusChangeResult> ApplyChangeAsync(
        Report report, ReportStatus nextStatus, int actorUserId, CancellationToken ct = default, CrewLocationUpdate? location = null)
    {

        if (!CanTransition(report.Status, nextStatus))
        {
            return new(
                StatusChangeOutcome.InvalidTransition,
                $"Cannot change status from {report.Status} to {nextStatus}.");
        }

        var previousStatus = report.Status;
        if (location != null && report.AssignedCrew is { } crew)
        {
            crew.LastLatitude = location.Latitude;
            crew.LastLongitude = location.Longitude;
            crew.LastSeenAtUtc = DateTime.UtcNow;
        }
        report.Status = nextStatus;

        var history = new ReportStatusHistory
        {
            ReportId = report.Id,
            Status = nextStatus,
            PreviousStatus = previousStatus,
            ChangedByUserId = actorUserId,
            ChangedAtUtc = DateTime.UtcNow
        };
        _db.ReportStatusHistories.Add(history);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent deletion can fail the history FK before EF executes the report update.
            if (ex is not DbUpdateConcurrencyException &&
                await _db.Reports.AsNoTracking().AnyAsync(r => r.Id == report.Id, ct))
                throw;

            // SaveChanges rolls back the history insert along with the losing update.
            _db.Entry(history).State = EntityState.Detached;
            await _db.Entry(report).ReloadAsync(ct);
            if (location != null && report.AssignedCrew is { } changedCrew) await _db.Entry(changedCrew).ReloadAsync(ct);
            return new(StatusChangeOutcome.Conflict,
                "The task status or assignment changed. Refresh the task before trying again.");
        }

        await _residentNotifier.NotifyAsync(report);

        return new(
            StatusChangeOutcome.Changed,
            $"Report status changed to {nextStatus}.");
    }
}
