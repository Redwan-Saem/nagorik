using Nagorik.Api.Models;
using Nagorik.Api.Services;
public class TransportFareTests
{
    [Fact] public void UsesMinimumAndEstimatedRangeWithoutInventingMissingFare()
    {
        var rules=new Dictionary<string,TransportFareRule>{{"Bus",new(){Mode="Bus",BaseBdt=5,PerKmBdt=3,MinimumBdt=12,UpperMultiplier=1.4,Source="Fixture"}}};
        Assert.Equal(new RouteFare(12,17,true,"Fixture"),TransportFareService.Calculate("Bus",1,false,rules));
        Assert.Equal(new RouteFare(35,49,true,"Fixture"),TransportFareService.Calculate("Bus",10,false,rules));
        Assert.Null(TransportFareService.Calculate("CNG",1,false,rules));Assert.Null(TransportFareService.Calculate("Bus",double.NaN,false,rules));
        rules["Bus"].IsExact=true;Assert.False(TransportFareService.Calculate("Bus",10,false,rules)!.Estimated);Assert.True(TransportFareService.Calculate("Bus",10,true,rules)!.Estimated);
    }
    [Fact] public async Task ContinuousBoardingChargesOnceAndMissingModeMakesTotalUnavailable()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await TransportSearchTests.Seed(db);
        db.TransportFareRules.Add(new(){Mode="Bus",BaseBdt=10,PerKmBdt=2,MinimumBdt=10,UpperMultiplier=1.5,Source="Fixture"});await db.SaveChangesAsync();
        var result=await new TransportSearchService(db).SearchAsync(TransportSearchTests.Input());
        var bus=Assert.Single(result.Options,x=>x.Legs.Count==1&&x.Legs[0].Mode=="Bus");Assert.Equal(24,bus.Fare!.MinimumBdt);
        Assert.All(result.Options.Where(x=>x.Legs.Any(l=>l.Mode=="CNG")),x=>Assert.Null(x.Fare));
    }
}
