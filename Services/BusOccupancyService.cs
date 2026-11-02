using Microsoft.EntityFrameworkCore;
namespace Nagorik.Api.Services;
public sealed record BusOccupancySummary(int LinkId,string Status,string? Level,DateTime? LastObservedAtUtc,int Reports,IReadOnlyDictionary<string,int> Distribution);
public sealed class BusOccupancyService(AppDbContext db,TimeProvider clock)
{
    public static readonly string[] Levels=["SeatsAvailable","Standing","Full"];
    public async Task<bool> LinkExists(int linkId,CancellationToken ct=default)=>await db.TransportLinks.AnyAsync(x=>x.Id==linkId&&x.Enabled&&x.Mode=="Bus"&&db.TransportStops.Any(s=>s.Id==x.FromStopId&&s.Enabled)&&db.TransportStops.Any(s=>s.Id==x.ToStopId&&s.Enabled),ct);
    public async Task<BusOccupancySummary> ReadAsync(int linkId,CancellationToken ct=default)
    {
        if(!await LinkExists(linkId,ct))throw new TransportProblem(404,"Enabled bus segment not found.");
        var now=clock.GetUtcNow().UtcDateTime;var cutoff=now.AddMinutes(-15);
        var rows=await db.BusOccupancyReports.AsNoTracking().Where(x=>x.LinkId==linkId&&x.ObservedAtUtc>cutoff&&x.ObservedAtUtc<=now).ToListAsync(ct);
        var distribution=Levels.ToDictionary(x=>x,x=>rows.Count(r=>r.Level==x));
        if(rows.Count==0)return new(linkId,"Unknown",null,null,0,distribution);
        var sorted=rows.Select(x=>Array.IndexOf(Levels,x.Level)).Order().ToArray();
        return new(linkId,"Recent community reports",Levels[sorted[sorted.Length/2]],DateTime.SpecifyKind(rows.Max(x=>x.ObservedAtUtc),DateTimeKind.Utc),rows.Count,distribution);
    }
    public async Task SubmitAsync(int resident,int linkId,string level,CancellationToken ct=default)
    {
        if(!Levels.Contains(level))throw new TransportProblem(400,"Choose SeatsAvailable, Standing or Full.");
        if(!await db.Users.AnyAsync(x=>x.Id==resident&&x.Role=="Resident",ct))throw new TransportProblem(403,"A provisioned resident account is required.");
        if(!await LinkExists(linkId,ct))throw new TransportProblem(404,"Enabled bus segment not found.");
        var now=clock.GetUtcNow().UtcDateTime;var cutoff=now.AddMinutes(-5);
        // One row per person/segment prevents repeated votes and makes cooldown atomic.
        var changed=await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO BusOccupancyReports (ResidentId, LinkId, Level, ObservedAtUtc) VALUES ({resident},{linkId},{level},{now}) ON CONFLICT(ResidentId,LinkId) DO UPDATE SET Level=excluded.Level, ObservedAtUtc=excluded.ObservedAtUtc WHERE BusOccupancyReports.ObservedAtUtc <= {cutoff}",ct);
        if(changed==0)throw new TransportProblem(429,"Please wait five minutes before updating this bus segment again.");
    }
}
