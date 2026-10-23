using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public sealed class ResidentScheduleQueryTests
{
    public sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private static readonly DateTimeOffset Now = new(2026,10,23,18,30,0,TimeSpan.Zero); // 00:30 on Oct 24 in Dhaka.
    private static async Task Seed(Nagorik.Api.AppDbContext db)
    {
        var actor=TestDb.AddUser(db);actor.Role="UtilityOfficial";
        db.Zones.Add(new(){Code="S1",Name="Dhaka zone",BoundaryGeoJson="{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}"});
        await db.SaveChangesAsync();
    }
    private static LoadsheddingSchedule Plan(DateOnly day, ScheduleState state=ScheduleState.Published) => new()
    {StartDate=day,EndDate=day,AvailableCapacityMw=10,CreatedByUserId=1,CreatedAtUtc=Now.UtcDateTime,
        State=state,PublishedByUserId=state==ScheduleState.Draft?null:1,PublishedAtUtc=state==ScheduleState.Draft?null:Now.UtcDateTime,
        Demands=[new(){ZoneId=1,DemandMw=10}]};

    [Fact]
    public async Task UsesDhakaMidnightAndDistinguishesUnpublishedFromNoOutages()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);
        var query=new ResidentScheduleQueryService(db,new FixedClock(Now));
        var empty=(await query.GetAsync(1))!;Assert.Equal(new DateOnly(2026,10,24),empty.Today);Assert.Equal(7,empty.Days.Count);
        Assert.Equal("NotPublished",empty.Status);Assert.Null(empty.LastUpdatedAtUtc);Assert.All(empty.Days,day=>Assert.False(day.IsPublished));
        var plan=Plan(empty.Today);db.LoadsheddingSchedules.Add(plan);await db.SaveChangesAsync();
        db.SchedulePublications.Add(new(){ZoneId=1,Date=empty.Today,ScheduleId=plan.Id});await db.SaveChangesAsync();
        var published=(await query.GetAsync(1))!;Assert.Equal("PartiallyPublished",published.Status);Assert.True(published.Days[0].IsPublished);
        Assert.Empty(published.Days[0].Outages);Assert.Null(published.NextOutage);Assert.NotNull(published.LastUpdatedAtUtc);
    }

    [Fact]
    public async Task DraftAndSupersededPointersCannotExposeOutages()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);
        var day=new DateOnly(2026,10,24);
        foreach(var state in new[]{ScheduleState.Draft,ScheduleState.Superseded})
        {
            var plan=Plan(day,state);plan.Slots.Add(new(){ZoneId=1,Date=day,StartMinute=0,EndMinute=120});
            db.LoadsheddingSchedules.Add(plan);await db.SaveChangesAsync();
            db.SchedulePublications.Add(new(){ZoneId=1,Date=day,ScheduleId=plan.Id});await db.SaveChangesAsync();
            var result=(await new ResidentScheduleQueryService(db,new FixedClock(Now)).GetAsync(1))!;
            Assert.Equal("NotPublished",result.Status);Assert.Null(result.NextOutage);Assert.All(result.Days,x=>Assert.Empty(x.Outages));
            await db.SchedulePublications.ExecuteDeleteAsync();db.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task ExactPublicationVersionAndOngoingOutageAreSelected()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);var day=new DateOnly(2026,10,24);
        var old=Plan(day);var current=Plan(day);
        old.Slots.Add(new(){ZoneId=1,Date=day,StartMinute=240,EndMinute=360});
        current.Slots.Add(new(){ZoneId=1,Date=day,StartMinute=0,EndMinute=120});
        db.LoadsheddingSchedules.AddRange(old,current);await db.SaveChangesAsync();
        db.SchedulePublications.Add(new(){ZoneId=1,Date=day,ScheduleId=current.Id});await db.SaveChangesAsync();
        var result=(await new ResidentScheduleQueryService(db,new FixedClock(Now)).GetAsync(1))!;
        Assert.Single(result.Days[0].Outages);Assert.True(result.NextOutage!.IsOngoing);Assert.Equal(0,result.NextOutage.StartMinute);
        Assert.Equal(new DateTime(2026,10,23,18,0,0,DateTimeKind.Utc),result.NextOutage.StartAtUtc);
        Assert.Equal(DateTimeKind.Utc,result.LastUpdatedAtUtc!.Value.Kind);
        Assert.Null(await new ResidentScheduleQueryService(db,new FixedClock(Now)).GetAsync(999));
    }
}
