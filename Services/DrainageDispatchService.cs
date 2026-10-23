using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;
public sealed record DrainageDispatchResult(AssignOutcome Outcome, string Message, int? ReportId = null);
public sealed class DrainageDispatchService(AppDbContext db, CrewAssignmentService assignment, TimeProvider clock)
{
    public async Task<DrainageDispatchResult> AssignAsync(int pointId, int crewId, int dispatcherId, bool confirmBusy, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var point = await db.DrainagePoints.Include(p => p.Zone).Include(p => p.LastWorkReport).SingleOrDefaultAsync(p => p.Id == pointId, ct);
        if (point == null) return new(AssignOutcome.ReportNotFound, "Drainage point not found.");
        if (point.Zone.RiskLevel is not (ZoneRiskLevel.High or ZoneRiskLevel.Severe) || point.Zone.LastComputedAtUtc == null ||
            point.Zone.LastComputedAtUtc < now.AddMinutes(-15) || point.Zone.LastComputedAtUtc > now.AddMinutes(1))
            return new(AssignOutcome.NotAssignable, "This point is no longer in a recently evaluated elevated-risk zone. Refresh the panel.");
        if (point.LastWorkReport?.Status is ReportStatus.Assigned or ReportStatus.InProgress)
            return new(AssignOutcome.Conflict, "An active task already exists for this drainage point.", point.LastWorkReportId);
        var crew = await db.Crews.SingleOrDefaultAsync(c => c.Id == crewId, ct);
        if (crew == null) return new(AssignOutcome.CrewNotFound, "Crew not found.");
        var busy = await db.Reports.AnyAsync(r => r.AssignedCrewId == crewId && (r.Status == ReportStatus.Assigned || r.Status == ReportStatus.InProgress), ct);
        if (busy && !confirmBusy) return new(AssignOutcome.NeedsConfirmation, "This crew is busy. Confirm the additional assignment explicitly.");
        var report = point.LastWorkReport;
        if (report == null || report.Status is ReportStatus.Completed or ReportStatus.Rejected)
        {
            report = new Report { UserId = dispatcherId, Title = $"Inspect drainage point #{point.Id}",
                Description = $"Dispatcher inspection request for a drainage point in {point.Zone.Code}; rule-based zone risk {point.Zone.RiskLevel}.",
                Category = ReportCategory.WaterLogging, Authority = Authority.CityCorporation, Status = ReportStatus.Approved,
                Location = $"Drainage point #{point.Id}, {point.Zone.Name}", Latitude = point.Latitude, Longitude = point.Longitude };
            point.LastWorkReport = report;
            // Save the work link before notifications. Competing inserts lose the point's
            // concurrency check and roll back their new report rather than duplicating work.
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); return new(AssignOutcome.Conflict, "Work changed concurrently. Refresh before assigning again."); }
        }
        var result = await assignment.AssignAsync(report.Id, crewId, dispatcherId, confirmBusy);
        return new(result.Outcome, result.Message, report.Id);
    }
}
