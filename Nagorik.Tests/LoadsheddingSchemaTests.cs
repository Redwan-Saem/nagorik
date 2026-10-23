using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Tests;

public sealed class LoadsheddingSchemaTests
{
    [Fact]
    public async Task DraftRoundTripsDemandSlotsAuditAndPublicationUniqueness()
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        TestDb.AddUser(db);
        var zone = new Zone { Code = "S1", Name = "Schedule zone", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" };
        db.Zones.Add(zone); await db.SaveChangesAsync();
        var schedule = new LoadsheddingSchedule { StartDate = new(2026, 10, 23), EndDate = new(2026, 10, 24), AvailableCapacityMw = 10, CreatedByUserId = 1, CreatedAtUtc = DateTime.UtcNow,
            Demands = [new() { ZoneId = zone.Id, DemandMw = 20 }],
            Slots = [new() { ZoneId = zone.Id, Date = new(2026, 10, 23), StartMinute = 0, EndMinute = 120 }] };
        db.LoadsheddingSchedules.Add(schedule); await db.SaveChangesAsync();
        db.ScheduleAudits.Add(new() { ScheduleId = schedule.Id, ActorUserId = 1, Action = "Generate", AtUtc = DateTime.UtcNow, Details = "Input snapshot saved" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.LoadsheddingSchedules.Include(x => x.Demands).Include(x => x.Slots).SingleAsync();
        Assert.Equal(ScheduleState.Draft, saved.State); Assert.Single(saved.Demands); Assert.Single(saved.Slots);
        Assert.Single(await db.ScheduleAudits.ToListAsync());
        db.SchedulePublications.Add(new() { ScheduleId = schedule.Id, ZoneId = zone.Id, Date = schedule.StartDate });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.SchedulePublications.Add(new() { ScheduleId = schedule.Id, ZoneId = zone.Id, Date = schedule.StartDate });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(-1, 0, 120)]
    [InlineData(10, -1, 120)]
    [InlineData(10, 120, 120)]
    [InlineData(10, 120, 1441)]
    public async Task InvalidCapacityOrSlotIsRejectedByDatabase(double capacity, int start, int end)
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        TestDb.AddUser(db);
        var zone = new Zone { Code = "S1", Name = "Zone", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" };
        var schedule = new LoadsheddingSchedule { StartDate = new(2026, 10, 23), EndDate = new(2026, 10, 24), AvailableCapacityMw = capacity, CreatedByUserId = 1,
            Slots = [new() { Zone = zone, Date = new(2026, 10, 23), StartMinute = start, EndMinute = end }] };
        db.LoadsheddingSchedules.Add(schedule);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
