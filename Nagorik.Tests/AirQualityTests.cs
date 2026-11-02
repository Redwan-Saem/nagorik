using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nagorik.Api.Services;
public class AirQualityTests
{
    sealed class Clock:TimeProvider{public DateTimeOffset Now=new(2026,10,23,16,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
    sealed class Provider(Clock clock):IAirQualityProvider
    {public bool Fail;public int Calls;public Task<AirQualityReading> FetchAsync(double a,double b,CancellationToken ct=default){Calls++;if(Fail)throw new HttpRequestException();return Task.FromResult(new AirQualityReading(133,60.1,63.8,clock.Now.UtcDateTime,clock.Now.UtcDateTime));}}
    [Fact]public void ParserRejectsMissingInvalidUnitsNegativeAndStaleValues()
    {
        var now=new Clock().Now.UtcDateTime;
        string Payload(string units="USAQI",double value=133,long? timestamp=null)=>JsonSerializer.Serialize(new{current_units=new{time="unixtime",us_aqi=units,pm2_5="μg/m³",pm10="μg/m³"},current=new{time=timestamp??new DateTimeOffset(now).ToUnixTimeSeconds(),us_aqi=value,pm2_5=60.1,pm10=63.8}});
        Assert.Equal(133,OpenMeteoAirQualityProvider.Parse(Payload(),now).UsAqi);
        Assert.Throws<FormatException>(()=>OpenMeteoAirQualityProvider.Parse(Payload("Other"),now));Assert.Throws<FormatException>(()=>OpenMeteoAirQualityProvider.Parse(Payload(value:-1),now));
        Assert.Throws<FormatException>(()=>OpenMeteoAirQualityProvider.Parse(Payload(timestamp:new DateTimeOffset(now.AddHours(-7)).ToUnixTimeSeconds()),now));
        Assert.Throws<KeyNotFoundException>(()=>OpenMeteoAirQualityProvider.Parse("{}",now));
    }
    [Fact]public async Task CachePreservesOriginalTimestampsOnFailureAndNeverTurnsMissingDataIntoGood()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await MunicipalScheduleTests.Seed(db);var clock=new Clock();var provider=new Provider(clock);using var memory=new MemoryCache(new MemoryCacheOptions());using var cache=new AirQualityCache(memory);
        var s=new AirQualityService(db,provider,clock,cache,Options.Create(new AirQualityOptions()),NullLogger<AirQualityService>.Instance);
        var first=await s.GetAsync(1);Assert.Equal("Model estimate",first.Status);await s.GetAsync(1);Assert.Equal(1,provider.Calls);
        clock.Now=clock.Now.AddMinutes(3);provider.Fail=true;var stale=await s.GetAsync(1);Assert.Equal("Stale",stale.Status);Assert.Equal(first.AtUtc,stale.AtUtc);Assert.Equal(first.FetchedAtUtc,stale.FetchedAtUtc);
        clock.Now=clock.Now.AddHours(7);var unknown=await s.GetAsync(1);Assert.Equal("Unknown",unknown.Status);Assert.Null(unknown.UsAqi);Assert.Null(unknown.Band);
    }
}
