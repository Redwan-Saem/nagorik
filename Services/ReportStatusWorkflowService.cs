using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public enum StatusChangeOutcome
{
    Changed,
    ReportNotFound,
    InvalidTransition
}

public record StatusChangeResult(
    StatusChangeOutcome Outcome,
    string Message);

public class ReportStatusWorkflowService
{
    private readonly AppDbContext _db;
    private readonly ResidentReportStatusNotifier _residentNotifier;

    public ReportStatusWorkflowService(AppDbContext db, ResidentReportStatusNotifier residentNotifier)
    {
        _db = db;
        _residentNotifier = residentNotifier;
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


    private async Task<StatusChangeResult> ApplyChangeAsync(
        Report report, ReportStatus nextStatus, int actorUserId, CancellationToken ct = default)
    {

        if (!CanTransition(report.Status, nextStatus))
        {
            return new(
                StatusChangeOutcome.InvalidTransition,
                $"Cannot change status from {report.Status} to {nextStatus}.");
        }

        var previousStatus = report.Status;
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

        await _db.SaveChangesAsync();

        await _residentNotifier.NotifyAsync(report);

        return new(
            StatusChangeOutcome.Changed,
            $"Report status changed to {nextStatus}.");
    }
}
