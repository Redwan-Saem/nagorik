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

    public ReportStatusWorkflowService(AppDbContext db)
    {
        _db = db;
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
        ReportStatus nextStatus)
    {
        var report = await _db.Reports
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null)
        {
            return new(
                StatusChangeOutcome.ReportNotFound,
                "Report not found.");
        }

        if (!CanTransition(report.Status, nextStatus))
        {
            return new(
                StatusChangeOutcome.InvalidTransition,
                $"Cannot change status from {report.Status} to {nextStatus}.");
        }

        report.Status = nextStatus;

        _db.ReportStatusHistories.Add(new ReportStatusHistory
        {
            ReportId = report.Id,
            Status = nextStatus,
            ChangedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return new(
            StatusChangeOutcome.Changed,
            $"Report status changed to {nextStatus}.");
    }
}