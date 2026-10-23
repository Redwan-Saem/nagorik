using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public sealed class LoadsheddingPublicationTests
{
    private static async Task<LoadsheddingScheduleService> Setup(Nagorik.Api.AppDbContext db)
    {
        var user = TestDb.AddUser(db); user.Role = "UtilityOfficial";
        db.Zones.AddRange(Enumerable.Range(1, 2).Select(id => new Zone { Code = $"S{id}", Name = $"Zone {id}",
            BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" }));
        await db.SaveChangesAsync();return new(db, new(), TimeProvider.System);
    }
    private static RotationInput Input(int[]? zones = null, int days = 2) => new(new(2026,10,23), new DateOnly(2026,10,23).AddDays(days - 1), 10,120,
        (zones ?? [1,2]).Select(id => new ZoneDemandInput(id,10)).ToArray());
    private static PublishScheduleRequest Confirm(int revision=1) => new(){ExpectedRevision=revision,Confirm=true,AcknowledgeWarnings=true};

    [Fact]
    public async Task PublishIsConfirmedAuditedAndMakesDraftImmutable()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var service=await Setup(db);
        var row=await service.GenerateAsync(Input(),1);
        Assert.Equal(400,(await Assert.ThrowsAsync<ScheduleProblem>(()=>service.PublishAsync(row.Id,new(){ExpectedRevision=1},1))).Status);
        await service.PublishAsync(row.Id,Confirm(),1);
        Assert.Equal(ScheduleState.Published,row.State);Assert.Equal(1,row.PublishedByUserId);Assert.NotNull(row.PublishedAtUtc);
        Assert.Equal(4,await db.SchedulePublications.CountAsync());Assert.Contains(await db.ScheduleAudits.ToListAsync(),x=>x.Action=="Publish");
        Assert.Equal(409,(await Assert.ThrowsAsync<ScheduleProblem>(()=>service.EditAsync(row.Id,new(){ExpectedRevision=2,SlotId=row.Slots.First().Id,Remove=true},1))).Status);
    }

    [Fact]
    public async Task PartialReplacementPreservesOtherZoneDaysAndFullReplacementSupersedes()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var service=await Setup(db);
        var first=await service.GenerateAsync(Input(),1);await service.PublishAsync(first.Id,Confirm(),1);
        var second=await service.GenerateAsync(Input([1],1),1);await service.PublishAsync(second.Id,Confirm(),1);
        Assert.Equal(second.Id,(await db.SchedulePublications.AsNoTracking().SingleAsync(p=>p.ZoneId==1&&p.Date==first.StartDate)).ScheduleId);
        Assert.Equal(3,await db.SchedulePublications.CountAsync(p=>p.ScheduleId==first.Id));
        var third=await service.GenerateAsync(Input(),1);await service.PublishAsync(third.Id,Confirm(),1);
        db.ChangeTracker.Clear();
        Assert.Equal(ScheduleState.Superseded,(await db.LoadsheddingSchedules.FindAsync(first.Id))!.State);
        Assert.Equal(ScheduleState.Superseded,(await db.LoadsheddingSchedules.FindAsync(second.Id))!.State);
        Assert.Equal(4,await db.SchedulePublications.CountAsync(p=>p.ScheduleId==third.Id));
        Assert.Equal(2,await db.ScheduleAudits.CountAsync(a=>a.Action=="Supersede"));
    }

    [Fact]
    public async Task UnsafeDraftAndUnacknowledgedWarningsCannotBePublished()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var service=await Setup(db);
        var row=await service.GenerateAsync(Input(),1);
        Assert.Equal(400,(await Assert.ThrowsAsync<ScheduleProblem>(()=>service.PublishAsync(row.Id,new(){ExpectedRevision=1,Confirm=true},1))).Status);
        db.ChangeTracker.Clear();row=await service.GetAsync(row.Id);
        Assert.Equal(1,row.Revision);
        await service.EditAsync(row.Id,new(){ExpectedRevision=1,SlotId=row.Slots.First().Id,Remove=true},1);
        Assert.Equal(400,(await Assert.ThrowsAsync<ScheduleProblem>(()=>service.PublishAsync(row.Id,Confirm(2),1))).Status);
        db.ChangeTracker.Clear();Assert.Empty(await db.SchedulePublications.ToListAsync());
        Assert.Equal(ScheduleState.Draft,(await db.LoadsheddingSchedules.SingleAsync()).State);
        Assert.DoesNotContain(await db.ScheduleAudits.ToListAsync(),x=>x.Action=="Publish");
    }
}
