using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed partial class LoadsheddingScheduleService
{
    public async Task<LoadsheddingSchedule> PublishAsync(int id, PublishScheduleRequest input, int actor, CancellationToken ct = default)
    {
        await ValidateActor(actor, ct);
        if (!input.Confirm) throw new ScheduleProblem(400, "Confirm publication explicitly.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await GetAsync(id, ct);
        await ClaimRevision(row, input.ExpectedRevision ?? 0, ct);
        var review = Review(row);
        if (!review.IsValid) throw new ScheduleProblem(400, string.Join(" ", review.Errors));
        if (review.Warnings.Count > 0 && !input.AcknowledgeWarnings)
            throw new ScheduleProblem(400, "Review and acknowledge the schedule warnings before publishing.");
        var zoneIds = row.Demands.Select(x => x.ZoneId).ToArray();
        var replaced = await db.SchedulePublications.Where(p => zoneIds.Contains(p.ZoneId) && p.Date >= row.StartDate && p.Date <= row.EndDate)
            .Select(p => p.ScheduleId).Distinct().ToListAsync(ct);
        row.State = ScheduleState.Published;row.PublishedByUserId = actor;row.PublishedAtUtc = clock.GetUtcNow().UtcDateTime;
        Audit(row, actor, "Publish", $"Publication confirmed. Warning acknowledgement={input.AcknowledgeWarnings}. Zone/day pointers replaced atomically.");
        await db.SaveChangesAsync(ct);
        foreach (var zoneId in zoneIds)
            for (var day = row.StartDate; day <= row.EndDate; day = day.AddDays(1))
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchedulePublications (ZoneId, Date, ScheduleId) VALUES ({zoneId}, {day}, {id}) ON CONFLICT(ZoneId, Date) DO UPDATE SET ScheduleId = excluded.ScheduleId", ct);
        foreach (var previous in replaced.Where(x => x != id))
        {
            if (await db.SchedulePublications.AnyAsync(p => p.ScheduleId == previous, ct)) continue;
            await db.LoadsheddingSchedules.Where(s => s.Id == previous && s.State == ScheduleState.Published)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.State, ScheduleState.Superseded).SetProperty(s => s.Revision, s => s.Revision + 1), ct);
            db.ScheduleAudits.Add(new() { ScheduleId = previous, ActorUserId = actor, Action = "Supersede",
                AtUtc = clock.GetUtcNow().UtcDateTime, Details = $"Replaced by schedule {id}; no current zone/day publications remain." });
        }
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        return row;
    }
}
