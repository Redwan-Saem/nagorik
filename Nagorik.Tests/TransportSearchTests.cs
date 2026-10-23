using Nagorik.Api.Models;
using Nagorik.Api.Services;
public class TransportSearchTests
{
    internal static async Task Seed(Nagorik.Api.AppDbContext db)
    {
        db.TransportStops.AddRange(new TransportStop{Id=1,Name="A",Latitude=23.75,Longitude=90.37,Source="Fixture"},new TransportStop{Id=2,Name="B",Latitude=23.76,Longitude=90.39,Source="Fixture"},new TransportStop{Id=3,Name="C",Latitude=23.74,Longitude=90.42,Source="Fixture"});await db.SaveChangesAsync();
        foreach(var mode in new[]{"Bus","CNG"})db.TransportLinks.AddRange(new TransportLink{FromStopId=1,ToStopId=2,Mode=mode,Line=mode,DistanceKm=3,Minutes=mode=="Bus"?20:10,Source="Fixture"},new TransportLink{FromStopId=2,ToStopId=3,Mode=mode,Line=mode,DistanceKm=4,Minutes=mode=="Bus"?25:15,Source="Fixture"});
        await db.SaveChangesAsync();
    }
    public static RouteSearchInput Input()=>new(){OriginLat=23.75,OriginLon=90.37,DestinationLat=23.74,DestinationLon=90.42};
    [Fact] public async Task DirectedGraphReturnsDistinctOptionsAndCoalescesSameBoarding()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await Seed(db);
        var timer=System.Diagnostics.Stopwatch.StartNew();var result=await new TransportSearchService(db).SearchAsync(Input());
        Assert.True(timer.Elapsed<TimeSpan.FromSeconds(5));Assert.True(result.Options.Count>=2);Assert.Contains(result.Options,x=>x.Legs.Count==1&&x.Legs[0].Mode=="Bus"&&x.DistanceKm==7&&x.Transfers==0);
        var reverse=Input();(reverse.OriginLat,reverse.DestinationLat)=(reverse.DestinationLat,reverse.OriginLat);(reverse.OriginLon,reverse.DestinationLon)=(reverse.DestinationLon,reverse.OriginLon);
        Assert.Empty((await new TransportSearchService(db).SearchAsync(reverse)).Options);
    }
    [Fact] public async Task InvalidAndUnsupportedPointsNeverProduceFabricatedRoutes()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var s=new TransportSearchService(db);
        var missing=await Assert.ThrowsAsync<TransportProblem>(()=>s.SearchAsync(new()));Assert.Equal(400,missing.Status);
        var input=Input();input.OriginLat=double.NaN;await Assert.ThrowsAsync<TransportProblem>(()=>s.SearchAsync(input));
        input=Input();input.DestinationLat=input.OriginLat;input.DestinationLon=input.OriginLon;await Assert.ThrowsAsync<TransportProblem>(()=>s.SearchAsync(input));
        input=Input();input.OriginLat=24;await Assert.ThrowsAsync<TransportProblem>(()=>s.SearchAsync(input));
        Assert.Equal("No routes found for these locations",(await s.SearchAsync(Input())).Message);
    }
}
