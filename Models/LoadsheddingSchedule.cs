namespace Nagorik.Api.Models;

public enum ScheduleState { Draft, Published, Superseded }

public sealed class LoadsheddingSchedule
{
    public int Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public double AvailableCapacityMw { get; set; }
    public int SlotMinutes { get; set; } = 120;
    public ScheduleState State { get; set; }
    public int Revision { get; set; } = 1;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int? PublishedByUserId { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public List<ScheduleZoneDemand> Demands { get; set; } = [];
    public List<LoadsheddingSlot> Slots { get; set; } = [];
}

// Demand is a draft input snapshot, never a fabricated measured zone demand.
public sealed class ScheduleZoneDemand
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public LoadsheddingSchedule Schedule { get; set; } = null!;
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public double DemandMw { get; set; }
}

public sealed class LoadsheddingSlot
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public LoadsheddingSchedule Schedule { get; set; } = null!;
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
}

// One current published version per zone/day. Publishing a partial overlap must
// not erase unaffected dates or zones from a previous publication.
public sealed class SchedulePublication
{
    public int ZoneId { get; set; }
    public DateOnly Date { get; set; }
    public int ScheduleId { get; set; }
    public LoadsheddingSchedule Schedule { get; set; } = null!;
}

public sealed class ScheduleAudit
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public LoadsheddingSchedule Schedule { get; set; } = null!;
    public int ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public string Details { get; set; } = "";
}
