using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Api.Data;

public static class DevelopmentTransportSeeder
{
    public static async Task SeedAsync(AppDbContext db,IHostEnvironment env,IConfiguration config,CancellationToken ct=default)
    {
        if(!env.IsDevelopment() || !config.GetValue("DevelopmentData:SeedTransportSamples",true) || await db.TransportStops.AnyAsync(ct)) return;
        const string source="Synthetic Dhaka demonstration catalog; not operator-verified";
        var stops=new[]{new TransportStop{Name="Dhanmondi 27",Latitude=23.7562,Longitude=90.3758},new TransportStop{Name="Farmgate",Latitude=23.7581,Longitude=90.3896},new TransportStop{Name="Shahbag",Latitude=23.7389,Longitude=90.3959},new TransportStop{Name="Motijheel",Latitude=23.7330,Longitude=90.4172},new TransportStop{Name="Mirpur 10",Latitude=23.8069,Longitude=90.3687}};
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        foreach(var s in stops){s.Source=source;s.IsSample=true;}db.TransportStops.AddRange(stops);await db.SaveChangesAsync(ct);
        foreach(var (a,b) in new[]{(0,1),(1,2),(2,3),(4,1)})
        foreach(var mode in new[]{"Bus","CNG"})
        foreach(var (from,to) in new[]{(a,b),(b,a)})
        {
            var km=GeoDistance.Meters(stops[from].Latitude,stops[from].Longitude,stops[to].Latitude,stops[to].Longitude)/1000*1.3;
            db.TransportLinks.Add(new(){FromStopId=stops[from].Id,ToStopId=stops[to].Id,Mode=mode,Line=mode+" sample corridor",DistanceKm=km,Minutes=(int)Math.Ceiling(km/(mode=="Bus"?12:18)*60)+2,Source=source,IsSample=true});
        }
        foreach(var (mode,baseFare,rate,min) in new[]{("Bus",0d,3d,10d),("CNG",40d,20d,60d),("Metro",20d,5d,20d),("Rickshaw",10d,25d,30d),("Walk",0d,0d,0d)})
            if(!await db.TransportFareRules.AnyAsync(x=>x.Mode==mode,ct))db.TransportFareRules.Add(new(){Mode=mode,BaseBdt=baseFare,PerKmBdt=rate,MinimumBdt=min,UpperMultiplier=mode=="Walk"?1:1.4,IsExact=mode=="Walk",Source=source,UpdatedAtUtc=DateTime.UtcNow});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
