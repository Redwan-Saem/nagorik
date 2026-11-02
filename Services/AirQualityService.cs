using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
namespace Nagorik.Api.Services;

public sealed class AirQualityOptions{public bool Enabled{get;set;}=true;}
public sealed record AirQualityReading(double UsAqi,double Pm25,double Pm10,DateTime AtUtc,DateTime FetchedAtUtc);
public interface IAirQualityProvider{Task<AirQualityReading> FetchAsync(double latitude,double longitude,CancellationToken ct=default);}
public sealed class OpenMeteoAirQualityProvider(HttpClient http,TimeProvider clock):IAirQualityProvider
{
    public const string Source="Open-Meteo / Copernicus CAMS global model";
    public async Task<AirQualityReading> FetchAsync(double latitude,double longitude,CancellationToken ct=default)
    {
        if(!double.IsFinite(latitude)||!double.IsFinite(longitude)||latitude is < -90 or >90||longitude is < -180 or >180)throw new FormatException("Invalid provider coordinates.");
        var uri=FormattableString.Invariant($"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={latitude:F5}&longitude={longitude:F5}&current=us_aqi,pm2_5,pm10&timezone=UTC&timeformat=unixtime&domains=cams_global");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var response=await http.GetAsync(uri,timeout.Token);response.EnsureSuccessStatusCode();
        var content=await response.Content.ReadAsStringAsync(timeout.Token);if(content.Length>128*1024)throw new FormatException("Oversized provider response.");
        return Parse(content,clock.GetUtcNow().UtcDateTime);
    }
    public static AirQualityReading Parse(string content,DateTime now)
    {
        using var json=JsonDocument.Parse(content);var root=json.RootElement;var units=root.GetProperty("current_units");
        if(units.GetProperty("time").GetString()!="unixtime"||units.GetProperty("us_aqi").GetString()!="USAQI"||units.GetProperty("pm2_5").GetString()!="μg/m³"||units.GetProperty("pm10").GetString()!="μg/m³")throw new FormatException("Unexpected air-quality units.");
        var data=root.GetProperty("current");var time=DateTimeOffset.FromUnixTimeSeconds(data.GetProperty("time").GetInt64()).UtcDateTime;
        double Value(string name,double max){var number=data.GetProperty(name).GetDouble();if(!double.IsFinite(number)||number<0||number>max)throw new FormatException("Invalid air-quality value.");return number;}
        if(time>now.AddMinutes(15)||time<now.AddHours(-6))throw new FormatException("Provider timestamp outside the supported freshness window.");
        return new(Value("us_aqi",2000),Value("pm2_5",5000),Value("pm10",10000),time,now);
    }
}
public sealed record AirQualityResult(int ZoneId,string ZoneName,string Status,string? Band,double? UsAqi,double? Pm25,double? Pm10,DateTime? AtUtc,DateTime? FetchedAtUtc,string Source,string Message);
public sealed class AirQualityCache(IMemoryCache memory):IDisposable
{
    public sealed record Entry(DateTime AttemptAtUtc,AirQualityReading? Reading,bool Failed);
    internal readonly SemaphoreSlim Gate=new(1,1);
    internal Entry? Get(string key)=>memory.Get<Entry>(key);
    internal void Set(string key,Entry entry)=>memory.Set(key,entry,TimeSpan.FromHours(6));
    public void Dispose()=>Gate.Dispose();
}
public sealed class AirQualityService(AppDbContext db,IAirQualityProvider provider,TimeProvider clock,AirQualityCache cache,IOptions<AirQualityOptions> options,ILogger<AirQualityService> logger)
{
    public static string Band(double value)=>value<=50?"Good":value<=100?"Moderate":value<=150?"Unhealthy for sensitive groups":value<=200?"Unhealthy":value<=300?"Very unhealthy":"Hazardous";
    public async Task<AirQualityResult> GetAsync(int zoneId,CancellationToken ct=default)
    {
        var zone=await db.Zones.AsNoTracking().Where(x=>x.Id==zoneId).Select(x=>new{x.Name,x.BoundaryGeoJson}).SingleOrDefaultAsync(ct)??throw new TransportProblem(404,"Zone not found.");
        AirQualityResult Result(AirQualityCache.Entry? entry,string? message=null)
        {
            var now=clock.GetUtcNow().UtcDateTime;var r=entry?.Reading;
            if(r==null||r.AtUtc<now.AddHours(-6)||r.AtUtc>now.AddMinutes(15))return new(zoneId,zone.Name,"Unknown",null,null,null,null,null,null,OpenMeteoAirQualityProvider.Source,message??"No usable provider data. Refresh later.");
            var stale=entry!.Failed||r.AtUtc<now.AddMinutes(-90);
            return new(zoneId,zone.Name,stale?"Stale":"Model estimate",Band(r.UsAqi),r.UsAqi,r.Pm25,r.Pm10,r.AtUtc,r.FetchedAtUtc,OpenMeteoAirQualityProvider.Source,message??(stale?"Older provider estimate; not current sensor data.":"Provider model estimate for a representative zone point, not a local sensor measurement."));
        }
        if(!options.Value.Enabled)return Result(null,"Air-quality provider is disabled.");
        if(!WeatherCollectionService.TryLocation(zone.BoundaryGeoJson,out var lat,out var lon))return Result(null,"This zone has no usable geographic boundary.");
        var key=FormattableString.Invariant($"aqi:{zoneId}:{lat:F5}:{lon:F5}");var entry=cache.Get(key);var now=clock.GetUtcNow().UtcDateTime;
        if(entry!=null&&entry.AttemptAtUtc<=now&&entry.AttemptAtUtc>now.AddMinutes(-2))return Result(entry);
        if(!await cache.Gate.WaitAsync(TimeSpan.FromSeconds(1),ct))return Result(entry==null?null:entry with{Failed=true},"Provider busy. Refresh later; any retained estimate keeps its original timestamp.");
        try
        {
            entry=cache.Get(key);now=clock.GetUtcNow().UtcDateTime;
            if(entry!=null&&entry.AttemptAtUtc<=now&&entry.AttemptAtUtc>now.AddMinutes(-2))return Result(entry);
            try{var reading=await provider.FetchAsync(lat,lon,ct);entry=new(now,reading,false);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
            catch(Exception e)when(e is HttpRequestException or OperationCanceledException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
            {logger.LogWarning("Air-quality provider unavailable for zone {ZoneId}: {ErrorType}",zoneId,e.GetType().Name);entry=new(now,entry?.Reading,true);}
            cache.Set(key,entry);return Result(entry);
        }
        finally{cache.Gate.Release();}
    }
}
