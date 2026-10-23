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

    [Fact]
    public async Task PublishedEmptyWeekHasNoOutagesRatherThanNotPublished()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);var day=new DateOnly(2026,10,24);
        var plan=Plan(day);plan.EndDate=day.AddDays(6);db.LoadsheddingSchedules.Add(plan);await db.SaveChangesAsync();
        db.SchedulePublications.AddRange(Enumerable.Range(0,7).Select(i=>new SchedulePublication{ZoneId=1,Date=day.AddDays(i),ScheduleId=plan.Id}));await db.SaveChangesAsync();
        var result=(await new ResidentScheduleQueryService(db,new FixedClock(Now)).GetAsync(1))!;
        Assert.Equal("NoOutages",result.Status);Assert.Null(result.NextOutage);Assert.All(result.Days,x=>Assert.True(x.IsPublished));
    }

    [Fact]
    public async Task PartialRepublishingUsesTheNewDayAndKeepsOtherPublishedDays()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);var day=new DateOnly(2026,10,24);
        db.Zones.Add(new(){Code="S2",Name="Second zone",BoundaryGeoJson="{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}"});await db.SaveChangesAsync();
        var service=new LoadsheddingScheduleService(db,new(),new FixedClock(Now));
        var old=await service.GenerateAsync(new(day,day.AddDays(6),10,120,[new(1,10),new(2,10)]),1);
        await service.PublishAsync(old.Id,new(){ExpectedRevision=1,Confirm=true,AcknowledgeWarnings=true},1);
        var replacement=await service.GenerateAsync(new(day,day,10,120,[new(1,10)]),1);
        await service.PublishAsync(replacement.Id,new(){ExpectedRevision=1,Confirm=true,AcknowledgeWarnings=true},1);
        var result=(await new ResidentScheduleQueryService(db,new FixedClock(Now)).GetAsync(1))!;
        Assert.Equal("Published",result.Status);Assert.True(result.Days[0].IsPublished);Assert.Empty(result.Days[0].Outages);
        Assert.All(result.Days.Skip(1),x=>{Assert.True(x.IsPublished);Assert.NotEmpty(x.Outages);});
    }

    [Fact]
    public async Task IndexedReadOfCurrentVersionFitsTwoSecondBudgetWithHistoricalSlots()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);var day=new DateOnly(2026,10,24);
        LoadsheddingSchedule? latest=null;
        for(var version=0;version<30;version++)
        {
            latest=Plan(day);latest.EndDate=day.AddDays(6);
            latest.Slots=Enumerable.Range(0,7).SelectMany(i=>Enumerable.Range(0,12).Select(slot=>new LoadsheddingSlot{ZoneId=1,Date=day.AddDays(i),StartMinute=slot*120,EndMinute=(slot+1)*120})).ToList();
            db.LoadsheddingSchedules.Add(latest);
        }
        await db.SaveChangesAsync();db.SchedulePublications.AddRange(Enumerable.Range(0,7).Select(i=>new SchedulePublication{ZoneId=1,Date=day.AddDays(i),ScheduleId=latest!.Id}));await db.SaveChangesAsync();
        var query=new ResidentScheduleQueryService(db,new FixedClock(Now));await query.GetAsync(1);
        var watch=System.Diagnostics.Stopwatch.StartNew();var result=(await query.GetAsync(1))!;watch.Stop();
        Assert.True(watch.Elapsed<TimeSpan.FromSeconds(2),$"Warm indexed query took {watch.Elapsed}");Assert.All(result.Days,x=>Assert.Equal(12,x.Outages.Count));
    }
}
