using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed class ScheduleProblem(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed partial class LoadsheddingScheduleService
{
    private readonly AppDbContext db;
    private readonly LoadsheddingRotationEngine engine;
    private readonly TimeProvider clock;
    public LoadsheddingScheduleService(AppDbContext db, LoadsheddingRotationEngine engine, TimeProvider clock)
    { this.db = db; this.engine = engine; this.clock = clock; }

    public async Task<LoadsheddingSchedule> GetAsync(int id, CancellationToken ct = default) =>
        await db.LoadsheddingSchedules.Include(x => x.Demands).ThenInclude(x => x.Zone)
            .Include(x => x.Slots).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ScheduleProblem(404, "Schedule not found.");

    public RotationReview Review(LoadsheddingSchedule row) => engine.Review(Input(row), row.Slots);
    private static RotationInput Input(LoadsheddingSchedule row) => new(row.StartDate, row.EndDate,
        row.AvailableCapacityMw, row.SlotMinutes, row.Demands.Select(x => new ZoneDemandInput(x.ZoneId, x.DemandMw)).ToArray());

    private async Task ValidateActor(int actor, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == actor &&
            (u.Role == "UtilityOfficial" || u.Role == "DPDCOfficial" || u.Role == "DESCOOfficial"), ct))
            throw new ScheduleProblem(403, "A provisioned utility official is required.");
    }

    private void Audit(LoadsheddingSchedule row, int actor, string action, string details) =>
        db.ScheduleAudits.Add(new() { Schedule = row, ActorUserId = actor, Action = action,
            AtUtc = clock.GetUtcNow().UtcDateTime, Details = details });

    public async Task<LoadsheddingSchedule> GenerateAsync(RotationInput input, int actor, CancellationToken ct = default)
    {
        LoadsheddingRotationEngine.ValidateInput(input); await ValidateActor(actor, ct);
        var ids = input.Zones.Select(x => x.ZoneId).ToArray();
        if (await db.Zones.CountAsync(z => ids.Contains(z.Id), ct) != ids.Length)
            throw new ScheduleProblem(400, "One or more selected zones no longer exist.");
        var row = new LoadsheddingSchedule { StartDate = input.StartDate, EndDate = input.EndDate,
            AvailableCapacityMw = input.AvailableCapacityMw, SlotMinutes = input.SlotMinutes,
            CreatedByUserId = actor, CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            Demands = input.Zones.Select(x => new ScheduleZoneDemand { ZoneId = x.ZoneId, DemandMw = x.DemandMw }).ToList(),
            Slots = engine.Generate(input).ToList() };
        db.LoadsheddingSchedules.Add(row);Audit(row, actor, "Generate", "Draft generated from stored period, capacity and per-zone demand snapshots.");
        await db.SaveChangesAsync(ct);
        return await GetAsync(row.Id, ct);
    }

    private async Task ClaimRevision(LoadsheddingSchedule row, int expected, CancellationToken ct)
    {
        if (expected <= 0 || row.Revision != expected || row.State != ScheduleState.Draft)
            throw new ScheduleProblem(409, "The draft changed or is no longer editable. Reload it.");
        var changed = await db.LoadsheddingSchedules.Where(x => x.Id == row.Id && x.Revision == expected && x.State == ScheduleState.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, expected + 1), ct);
        if (changed != 1) throw new ScheduleProblem(409, "Another official changed this draft. Reload it.");
        row.Revision = expected + 1;
        var property = db.Entry(row).Property(x => x.Revision);
        property.OriginalValue = row.Revision; property.IsModified = false;
    }

    public async Task<LoadsheddingSchedule> EditAsync(int id, EditScheduleSlotRequest input, int actor, CancellationToken ct = default)
    {
        await ValidateActor(actor, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await GetAsync(id, ct);
        await ClaimRevision(row, input.ExpectedRevision ?? 0, ct);
        var slot = input.SlotId == 0 ? null : row.Slots.SingleOrDefault(x => x.Id == input.SlotId)
            ?? throw new ScheduleProblem(404, "The slot does not belong to this schedule.");
        if (input.Remove)
        {
            if (slot == null) throw new ScheduleProblem(400, "Select an existing slot to remove.");
            row.Slots.Remove(slot);db.LoadsheddingSlots.Remove(slot);
        }
        else
        {
            if (input.Date == null) throw new ScheduleProblem(400, "Choose a date.");
            slot ??= new LoadsheddingSlot { Schedule = row };
            slot.ZoneId = input.ZoneId;slot.Date = input.Date.Value;slot.StartMinute = input.StartMinute;slot.EndMinute = input.EndMinute;
            if (input.SlotId == 0) row.Slots.Add(slot);
        }
        var review = Review(row);
        var structural = review.Errors.Where(e => !e.StartsWith("Supplied demand exceeds", StringComparison.Ordinal)).ToArray();
        if (structural.Length > 0) throw new ScheduleProblem(400, string.Join(" ", structural));
        Audit(row, actor, "Edit", $"Slot {input.SlotId}; remove={input.Remove}; zone={input.ZoneId}; date={input.Date}; start={input.StartMinute}; end={input.EndMinute}.");
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        // Drafts may retain capacity errors while the official makes a sequence of
        // edits. Review is returned and publication refuses every such error.
        return row;
    }
}
