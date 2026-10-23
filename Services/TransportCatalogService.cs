using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Services;
public sealed class TransportCatalogService(AppDbContext db,TimeProvider clock)
{
    private static readonly string[] Modes=["Bus","Metro","CNG","Rickshaw","Walk"];
    public async Task<TransportCatalogDocument> ExportAsync(CancellationToken ct=default)=>new()
    {
        ExpectedRevision=await db.TransportCatalogStates.Select(x=>x.Revision).SingleOrDefaultAsync(ct),
        Source="",
        Stops=await db.TransportStops.AsNoTracking().Where(x=>x.Enabled).OrderBy(x=>x.Id).ToListAsync(ct),
        Links=await db.TransportLinks.AsNoTracking().Where(x=>x.Enabled).OrderBy(x=>x.Id).ToListAsync(ct),
        FareRules=await db.TransportFareRules.AsNoTracking().OrderBy(x=>x.Mode).ToListAsync(ct)
    };
    public async Task<int> ImportAsync(TransportCatalogDocument input,string actor,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(input.Source)||input.Source.Length>300||input.ExpectedRevision<0||input.Stops==null||input.Links==null||input.FareRules==null||input.Stops.Count is <1 or >500||input.Links.Count>5000||input.FareRules.Count>5)throw new TransportProblem(400,"Supply a source, current revision, 1–500 stops, at most 5,000 directed links and five fare rules.");
        static bool Text(string? s,int max)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=max;
        if(input.Stops.Any(x=>x.Id<=0||!Text(x.Name,160)||!Text(x.Source,500)||!TransportSearchService.Inside(x.Latitude,x.Longitude))||input.Stops.Select(x=>x.Id).Distinct().Count()!=input.Stops.Count)throw new TransportProblem(400,"Stops need unique positive IDs, names, sources and valid Dhaka coordinates.");
        var stopIds=input.Stops.Select(x=>x.Id).ToArray();var stopMap=input.Stops.ToDictionary(x=>x.Id);
        if(input.Links.Any(x=>x.Id<=0||!stopMap.ContainsKey(x.FromStopId)||!stopMap.ContainsKey(x.ToStopId)||x.FromStopId==x.ToStopId||!Modes.Contains(x.Mode)||!Text(x.Source,500)||x.Line?.Length>100||!double.IsFinite(x.DistanceKm)||x.DistanceKm<=0||x.DistanceKm>100||x.Minutes is <1 or >300)||input.Links.Select(x=>x.Id).Distinct().Count()!=input.Links.Count)throw new TransportProblem(400,"Links need unique positive IDs, included stop IDs, a known mode, source, positive distance and duration.");
        foreach(var link in input.Links)
        {
            var a=stopMap[link.FromStopId];var b=stopMap[link.ToStopId];
            if(link.DistanceKm<GeoDistance.Meters(a.Latitude,a.Longitude,b.Latitude,b.Longitude)/1000*.95)throw new TransportProblem(400,"Link distance cannot be shorter than its straight-line stop separation.");
        }
        if(input.FareRules.Select(x=>x.Mode).Distinct().Count()!=input.FareRules.Count||input.FareRules.Any(x=>!Modes.Contains(x.Mode)||!Text(x.Source,500)||new[]{x.BaseBdt,x.PerKmBdt,x.MinimumBdt,x.UpperMultiplier}.Any(v=>!double.IsFinite(v))||x.BaseBdt is <0 or >10000||x.PerKmBdt is <0 or >10000||x.MinimumBdt is <0 or >10000||x.UpperMultiplier is <1 or >10))throw new TransportProblem(400,"Fare rules need unique known modes, finite nonnegative BDT coefficients, a 1–10 range multiplier and source.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var state=await db.TransportCatalogStates.SingleOrDefaultAsync(ct);
        if((state?.Revision??0)!=input.ExpectedRevision)throw new TransportProblem(409,"Transport catalog changed. Export again before importing.");
        var linkIds=input.Links.Select(x=>x.Id).ToArray();
        await db.TransportLinks.Where(x=>!linkIds.Contains(x.Id)).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Enabled,false),ct);
        await db.TransportStops.Where(x=>!stopIds.Contains(x.Id)).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Enabled,false),ct);
        var stops=await db.TransportStops.Where(x=>stopIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        var links=await db.TransportLinks.Where(x=>linkIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        foreach(var stop in input.Stops){if(stops.TryGetValue(stop.Id,out var row))db.Entry(row).CurrentValues.SetValues(stop);else db.TransportStops.Add(stop);}
        foreach(var link in input.Links){link.Line??="";if(links.TryGetValue(link.Id,out var row))db.Entry(row).CurrentValues.SetValues(link);else db.TransportLinks.Add(link);}
        var modes=input.FareRules.Select(x=>x.Mode).ToArray();await db.TransportFareRules.Where(x=>!modes.Contains(x.Mode)).ExecuteDeleteAsync(ct);
        var rules=await db.TransportFareRules.ToDictionaryAsync(x=>x.Mode,ct);
        foreach(var rule in input.FareRules){rule.UpdatedAtUtc=clock.GetUtcNow().UtcDateTime;if(rules.TryGetValue(rule.Mode,out var row))db.Entry(row).CurrentValues.SetValues(rule);else db.TransportFareRules.Add(rule);}
        if(state==null){state=new();db.TransportCatalogStates.Add(state);}state.Revision++;
        db.ManagedDataChanges.Add(new(){Kind="Transport",ActorUserId=actor,AtUtc=clock.GetUtcNow().UtcDateTime,Source=input.Source.Trim(),IsSample=input.Stops.Any(x=>x.IsSample)||input.Links.Any(x=>x.IsSample),PayloadJson=JsonSerializer.Serialize(input)});
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){throw new TransportProblem(409,"Catalog changed or failed database constraints. Export current data and retry.");}
        await tx.CommitAsync(ct);return state.Revision;
    }
}
