using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Services;

public sealed class RouteSearchInput
{
    [Required] public double? OriginLat { get; set; }
    [Required] public double? OriginLon { get; set; }
    [Required] public double? DestinationLat { get; set; }
    [Required] public double? DestinationLon { get; set; }
    public string Sort { get; set; } = "fastest";
}
public sealed record RoutePoint(string Name,double Latitude,double Longitude);
public sealed record RouteFare(double MinimumBdt,double MaximumBdt,bool Estimated,string Source);
public sealed record RouteLeg(string Mode,string Line,RoutePoint From,RoutePoint To,double DistanceKm,int Minutes,string Direction,bool IsSample,string Source,RouteFare? Fare=null,IReadOnlyList<RoutePoint>? Geometry=null);
public sealed record TransportOption(string Id,IReadOnlyList<RouteLeg> Legs,int Transfers,int Minutes,double DistanceKm,bool IsSample,RouteFare? Fare=null);
public sealed record RouteSearchResult(IReadOnlyList<TransportOption> Options,string Message,bool Truncated=false);
public sealed class TransportProblem(int status,string message):Exception(message){ public int Status{get;}=status; }

public sealed partial class TransportSearchService(AppDbContext db)
{
    public static bool Inside(double lat,double lon)=>double.IsFinite(lat)&&double.IsFinite(lon)&&lat>=23.60&&lat<=23.95&&lon>=90.20&&lon<=90.58;
    public static void Validate(RouteSearchInput input)
    {
        if(input.OriginLat is not {} a||input.OriginLon is not {} b||input.DestinationLat is not {} c||input.DestinationLon is not {} d)
            throw new TransportProblem(400,"Both origin and destination coordinates are required. Choose a suggestion, map pin or GPS.");
        if(!Inside(a,b)||!Inside(c,d))throw new TransportProblem(400,"Locations must be inside the Dhaka service area (23.60–23.95 N, 90.20–90.58 E).");
        if(GeoDistance.Meters(a,b,c,d)<50)throw new TransportProblem(400,"Origin and destination must be at least 50 metres apart.");
        if(input.Sort is not ("fastest" or "cheapest" or "transfers"))throw new TransportProblem(400,"Choose fastest, cheapest or transfers sorting.");
    }
    private sealed record State(int Stop,List<TransportLink> Links,HashSet<int> Visited,int Minutes);
    private static RoutePoint Point(TransportStop stop)=>new(stop.Name,stop.Latitude,stop.Longitude);
    private static RouteLeg Walk(RoutePoint a,RoutePoint b)
    {
        var km=GeoDistance.Meters(a.Latitude,a.Longitude,b.Latitude,b.Longitude)/1000;
        return new("Walk","",a,b,Math.Round(km,3),(int)Math.Ceiling(km/4*60),$"Walk from {a.Name} to {b.Name}; verify a safe walking path.",false,"Straight-line walking estimate");
    }
    public async Task<RouteSearchResult> SearchAsync(RouteSearchInput input,CancellationToken ct=default)
    {
        Validate(input);var timer=Stopwatch.StartNew();
        var stops=await db.TransportStops.AsNoTracking().Where(x=>x.Enabled).OrderBy(x=>x.Id).Take(501).ToListAsync(ct);
        var links=await db.TransportLinks.AsNoTracking().Where(x=>x.Enabled).OrderBy(x=>x.Id).Take(5001).ToListAsync(ct);
        if(stops.Count>500||links.Count>5000)throw new TransportProblem(503,"Transport catalog exceeds the supported search size. Ask an administrator to review it.");
        var byId=stops.Where(s=>Inside(s.Latitude,s.Longitude)).ToDictionary(x=>x.Id);
        var origin=new RoutePoint("Origin",input.OriginLat!.Value,input.OriginLon!.Value);var destination=new RoutePoint("Destination",input.DestinationLat!.Value,input.DestinationLon!.Value);
        var starts=byId.Values.Select(s=>(Stop:s,Walk:Walk(origin,Point(s)))).Where(x=>x.Walk.DistanceKm<=1.2).OrderBy(x=>x.Walk.DistanceKm).Take(2).ToArray();
        var ends=byId.Values.Select(s=>(Stop:s,Walk:Walk(Point(s),destination))).Where(x=>x.Walk.DistanceKm<=1.2).OrderBy(x=>x.Walk.DistanceKm).Take(2).ToDictionary(x=>x.Stop.Id,x=>x.Walk);
        var adjacency=links.Where(l=>byId.ContainsKey(l.FromStopId)&&byId.ContainsKey(l.ToStopId)).GroupBy(x=>x.FromStopId).ToDictionary(x=>x.Key,x=>x.ToArray());
        var found=new List<TransportOption>();var signatures=new HashSet<string>();var expanded=0;bool truncated=false;
        foreach(var start in starts)
        {
            var queue=new PriorityQueue<State,int>();queue.Enqueue(new(start.Stop.Id,[],[start.Stop.Id],start.Walk.Minutes),start.Walk.Minutes);
            while(queue.TryDequeue(out var state,out _))
            {
                ct.ThrowIfCancellationRequested();if(++expanded>20000||timer.Elapsed>TimeSpan.FromSeconds(4)){truncated=true;break;}
                if(state.Links.Count>0 && ends.TryGetValue(state.Stop,out var finalWalk))
                {
                    var signature=string.Join('-',state.Links.Select(l=>l.Id));
                    if(signatures.Add(signature))
                    {
                        var legs=new List<RouteLeg>();if(start.Walk.DistanceKm>.005)legs.Add(start.Walk);
                        foreach(var l in state.Links)
                        {
                            var from=Point(byId[l.FromStopId]);var to=Point(byId[l.ToStopId]);
                            if(legs.LastOrDefault() is {} previous && previous.Mode==l.Mode&&previous.Line==l.Line&&previous.To==from)
                                legs[^1]=previous with{To=to,DistanceKm=previous.DistanceKm+l.DistanceKm,Minutes=previous.Minutes+l.Minutes,IsSample=previous.IsSample||l.IsSample,Source=previous.Source==l.Source?l.Source:"Multiple catalog sources",Direction=$"Take {l.Mode} {l.Line} from {previous.From.Name} to {to.Name}.",Geometry=[..(previous.Geometry??[previous.From,previous.To]),to]};
                            else legs.Add(new(l.Mode,l.Line,from,to,l.DistanceKm,l.Minutes,$"Take {l.Mode} {l.Line} from {from.Name} to {to.Name}.",l.IsSample,l.Source,Geometry:[from,to]));
                        }
                        if(finalWalk.DistanceKm>.005)legs.Add(finalWalk);
                        var boardings=legs.Count(x=>x.Mode!="Walk");
                        var key=$"{input.OriginLat:R},{input.OriginLon:R}:{input.DestinationLat:R},{input.DestinationLon:R}:{signature}";
                        var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..20].ToLowerInvariant();
                        found.Add(new(id,legs,Math.Max(0,boardings-1),legs.Sum(x=>x.Minutes),Math.Round(legs.Sum(x=>x.DistanceKm),2),legs.Any(x=>x.IsSample)));
                    }
                    if(found.Count>=48)break;
                    continue;
                }
                if(state.Links.Count>=10||!adjacency.TryGetValue(state.Stop,out var next))continue;
                foreach(var l in next.Where(l=>!state.Visited.Contains(l.ToStopId)))
                {
                    var transfer=state.Links.LastOrDefault() is {} prev && (prev.Mode!=l.Mode||prev.Line!=l.Line)?5:0;
                    var minutes=state.Minutes+l.Minutes+transfer;
                    queue.Enqueue(new(l.ToStopId,[..state.Links,l],[..state.Visited,l.ToStopId],minutes),minutes);
                }
            }
            if(truncated)break;
        }
        // Transfer waiting time is explicit in totals; fares are added separately.
        var rules=await db.TransportFareRules.AsNoTracking().ToDictionaryAsync(x=>x.Mode,ct);
        found=found.Select(x=>TransportFareService.Price(x with{Minutes=x.Minutes+x.Transfers*5},rules)).ToList();
        return new(Sort(found,input.Sort).Take(12).ToArray(),found.Count==0?"No routes found for these locations":"Options use catalog estimates; verify services before travel.",truncated);
    }
    public static IOrderedEnumerable<TransportOption> Sort(IEnumerable<TransportOption> options,string sort)=>sort switch
    {
        "cheapest"=>options.OrderBy(x=>x.Fare==null).ThenBy(x=>x.Fare?.MinimumBdt??double.MaxValue).ThenBy(x=>x.Minutes).ThenBy(x=>x.Id),
        "transfers"=>options.OrderBy(x=>x.Transfers).ThenBy(x=>x.Minutes).ThenBy(x=>x.Id),
        _=>options.OrderBy(x=>x.Minutes).ThenBy(x=>x.Transfers).ThenBy(x=>x.Id)
    };
}
