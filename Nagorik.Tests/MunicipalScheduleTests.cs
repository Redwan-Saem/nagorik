using Nagorik.Api.Models;
using Nagorik.Api.Services;
public class MunicipalScheduleTests
{
    sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>new(2026,10,25,19,0,0,TimeSpan.Zero);}
    internal static MunicipalScheduleInput Input()=>new(){ZoneId=1,FromDate=new(2026,10,26),ToDate=new(2026,11,1),DaysMask=2,StartMinute=480,EndMinute=540,Source="District bulletin (fixture)"};
    internal static async Task Seed(Nagorik.Api.AppDbContext db)
    {
        var water=TestDb.AddUser(db,1);water.Role="UtilityOfficial";var city=TestDb.AddUser(db,2);city.Role="Official";
        db.Zones.Add(new(){Id=1,Code="Q1",Name="Fixture zone",BoundaryGeoJson="{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}"});await db.SaveChangesAsync();
    }
    [Theory][InlineData("Water",1)][InlineData("Waste",2)]
    public async Task DraftsStayPrivateAndWeeklyPublicationRequiresConfirmationAndCurrentRevision(string kind,int actor)
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);var service=new MunicipalScheduleService(db,new Clock());
        var row=await service.SaveDraftAsync(kind,null,Input(),actor);Assert.All((await service.WeekAsync(kind,1)).Days,x=>Assert.False(x.IsPublished));
        Assert.Equal(400,(await Assert.ThrowsAsync<TransportProblem>(()=>service.ChangeStateAsync(kind,row.Id,1,true,false,actor))).Status);
        await service.ChangeStateAsync(kind,row.Id,1,true,true,actor);var week=await service.WeekAsync(kind,1);
        Assert.Equal(new DateOnly(2026,10,26),week.Days[0].Date);Assert.Single(week.Days[0].Windows);Assert.True(week.Days[1].IsPublished);Assert.Empty(week.Days[1].Windows);
        Assert.Equal(409,(await Assert.ThrowsAsync<TransportProblem>(()=>service.SaveDraftAsync(kind,row.Id,Input(),actor))).Status);
        var overlap=await service.SaveDraftAsync(kind,null,Input(),actor);Assert.Equal(409,(await Assert.ThrowsAsync<TransportProblem>(()=>service.ChangeStateAsync(kind,overlap.Id,1,true,true,actor))).Status);
        Assert.Equal(409,(await Assert.ThrowsAsync<TransportProblem>(()=>service.ChangeStateAsync(kind,row.Id,1,false,true,actor))).Status);
        await service.ChangeStateAsync(kind,row.Id,2,false,true,actor);Assert.All((await service.WeekAsync(kind,1)).Days,x=>Assert.False(x.IsPublished));
    }
    [Theory][InlineData("Water",2)][InlineData("Waste",1)]
    public async Task WrongServiceOfficialAndInvalidWindowsCannotWrite(string kind,int wrongActor)
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);var service=new MunicipalScheduleService(db,new Clock());
        Assert.Equal(403,(await Assert.ThrowsAsync<TransportProblem>(()=>service.SaveDraftAsync(kind,null,Input(),wrongActor))).Status);
        var input=Input();input.EndMinute=input.StartMinute;Assert.Equal(400,(await Assert.ThrowsAsync<TransportProblem>(()=>service.SaveDraftAsync(kind,null,input,kind=="Water"?1:2))).Status);
    }
}
