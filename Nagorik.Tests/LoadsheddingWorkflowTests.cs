using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public sealed class LoadsheddingWorkflowTests
{
    private static LoadsheddingScheduleService Service(Nagorik.Api.AppDbContext db) => new(db, new(), TimeProvider.System);
    private static RotationInput Input(int[] ids, DateOnly? day = null) => new(day ?? new(2026,10,23), day ?? new(2026,10,23), 10, 120, ids.Select(id=>new ZoneDemandInput(id,10)).ToArray());
    private static async Task<int[]> Seed(Nagorik.Api.AppDbContext db)
    {
        var user=TestDb.AddUser(db);user.Role="UtilityOfficial";
        db.Zones.AddRange(Enumerable.Range(1,2).Select(id=>new Zone{Code=$"S{id}",Name=$"Zone {id}",BoundaryGeoJson="{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}"}));
        await db.SaveChangesAsync();return await db.Zones.OrderBy(z=>z.Id).Select(z=>z.Id).ToArrayAsync();
    }

    [Fact]
    public async Task GeneratePersistsDraftSnapshotAndAuditWithoutPublishing()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var ids=await Seed(db);
        var row=await Service(db).GenerateAsync(Input(ids),1);
        Assert.Equal(ScheduleState.Draft,row.State);Assert.Null(row.PublishedAtUtc);Assert.Empty(await db.SchedulePublications.ToListAsync());
        Assert.Equal(2,row.Demands.Count);Assert.Equal(12,row.Slots.Count);Assert.Equal("Generate",(await db.ScheduleAudits.SingleAsync()).Action);
    }

    [Fact]
    public async Task InvalidZoneAndNonUtilityActorCannotGenerate()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);
        Assert.Equal(400,(await Assert.ThrowsAsync<ScheduleProblem>(()=>Service(db).GenerateAsync(Input([999]),1))).Status);
        TestDb.AddUser(db,2);Assert.Equal(403,(await Assert.ThrowsAsync<ScheduleProblem>(()=>Service(db).GenerateAsync(Input([1]),2))).Status);
        Assert.Empty(await db.LoadsheddingSchedules.ToListAsync());
    }

    [Fact]
    public async Task EditAdvancesRevisionAndStaleWriteIsRejected()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var ids=await Seed(db);var service=Service(db);
        var row=await service.GenerateAsync(Input(ids),1);var slot=row.Slots.First();
        await service.EditAsync(row.Id,new(){ExpectedRevision=1,SlotId=slot.Id,ZoneId=slot.ZoneId,Date=slot.Date,StartMinute=slot.StartMinute,EndMinute=slot.EndMinute},1);
        Assert.Equal(2,row.Revision);Assert.Equal(2,await db.ScheduleAudits.CountAsync());
        Assert.Equal(409,(await Assert.ThrowsAsync<ScheduleProblem>(()=>service.EditAsync(row.Id,new(){ExpectedRevision=1,SlotId=slot.Id,Remove=true},1))).Status);
    }

    [Fact]
    public async Task RemovingOutageMaySaveDraftButReviewShowsUnsafeCapacity()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var ids=await Seed(db);var service=Service(db);
        var row=await service.GenerateAsync(Input(ids),1);
        await service.EditAsync(row.Id,new(){ExpectedRevision=1,SlotId=row.Slots.First().Id,Remove=true},1);
        Assert.False(service.Review(row).IsValid);Assert.Empty(await db.SchedulePublications.ToListAsync());
    }

    [Fact]
    public async Task IndependentlyLoadedStaleContextCannotOverwriteNewRevision()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var ids=await Seed(db);
        var first=Service(db);var row=await first.GenerateAsync(Input(ids),1);var slot=row.Slots.First();
        await using var other=new Nagorik.Api.AppDbContext(new DbContextOptionsBuilder<Nagorik.Api.AppDbContext>().UseSqlite(c).Options);
        var second=Service(other);await second.GetAsync(row.Id);
        var request=new EditScheduleSlotRequest{ExpectedRevision=1,SlotId=slot.Id,ZoneId=slot.ZoneId,Date=slot.Date,StartMinute=slot.StartMinute,EndMinute=slot.EndMinute};
        await first.EditAsync(row.Id,request,1);
        Assert.Equal(409,(await Assert.ThrowsAsync<ScheduleProblem>(()=>second.EditAsync(row.Id,request,1))).Status);
        Assert.Equal(2,await db.ScheduleAudits.CountAsync());
    }
}
