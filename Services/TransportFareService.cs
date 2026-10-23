using Nagorik.Api.Models;
namespace Nagorik.Api.Services;

public static class TransportFareService
{
    public static RouteFare? Calculate(string mode,double km,bool sample,IReadOnlyDictionary<string,TransportFareRule> rules)
    {
        if(!double.IsFinite(km)||km<0)return null;
        if(mode=="Walk")return new(0,0,false,"Walking has no fare");
        if(!rules.TryGetValue(mode,out var r)||!double.IsFinite(r.BaseBdt)||!double.IsFinite(r.PerKmBdt)||!double.IsFinite(r.MinimumBdt)||!double.IsFinite(r.UpperMultiplier)||r.BaseBdt<0||r.PerKmBdt<0||r.MinimumBdt<0||r.UpperMultiplier<1)return null;
        var minimum=Math.Ceiling(Math.Max(r.MinimumBdt,r.BaseBdt+r.PerKmBdt*km));
        var estimated=!r.IsExact||sample;
        return new(minimum,estimated?Math.Ceiling(minimum*r.UpperMultiplier):minimum,estimated,r.Source);
    }
    public static TransportOption Price(TransportOption option,IReadOnlyDictionary<string,TransportFareRule> rules)
    {
        var legs=option.Legs.Select(l=>l with{Fare=Calculate(l.Mode,l.DistanceKm,l.IsSample,rules)}).ToArray();
        RouteFare? total=legs.Any(l=>l.Fare==null)?null:new(legs.Sum(l=>l.Fare!.MinimumBdt),legs.Sum(l=>l.Fare!.MaximumBdt),legs.Any(l=>l.Fare!.Estimated),string.Join("; ",legs.Select(l=>l.Fare!.Source).Distinct()));
        return option with{Legs=legs,Fare=total};
    }
}
