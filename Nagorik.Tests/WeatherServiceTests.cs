using System.Net;
using System.Text.Json;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public class WeatherServiceTests
{
    public static readonly DateTime Hour = new(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
    public static string Payload(string? invalid = null)
    {
        if (invalid == "root") return "[]";
        if (invalid == "units-type") return "{\"hourly_units\":{\"rain\":42,\"showers\":\"mm\",\"time\":\"unixtime\"}}";
        var anchor = new DateTimeOffset(Hour).ToUnixTimeSeconds();
        var times = Enumerable.Range(-5, 12).Select(i => anchor + i * 3600).ToArray();
        object?[] rain = Enumerable.Range(1, 12).Select(i => (object?)i).ToArray();
        if (invalid == "missing") rain[0] = null;
        if (invalid == "negative") rain[0] = -1;
        if (invalid == "duplicate") times[1] = times[0];
        if (invalid == "gap") times[0] -= 3600;
        return JsonSerializer.Serialize(new { hourly_units = new { time = "unixtime", rain = invalid == "units" ? "inch" : "mm", showers = "mm" },
            hourly = new { time = times, rain, showers = Enumerable.Repeat(0.5, 12).ToArray() } });
    }
    public sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return send(request, ct); }
    }

    [Fact]
    public async Task RequestsUtcRainAndShowersAndSeparatesPastFromFutureWindows()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("api.open-meteo.com", request.RequestUri!.Host);
            Assert.Contains("hourly=rain,showers", request.RequestUri.Query);
            Assert.Contains("timeformat=unixtime", request.RequestUri.Query);
            Assert.Contains("timezone=UTC", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Payload()) });
        });
        using var client = new HttpClient(handler);
        var result = await new OpenMeteoWeatherService(client).FetchAsync(23.74, 90.37, Hour);
        Assert.Equal(Hour, result.TimestampUtc);
        Assert.Equal(6.5, result.Past.OneHourMm); Assert.Equal(16.5, result.Past.ThreeHoursMm); Assert.Equal(24, result.Past.SixHoursMm);
        Assert.Equal(7.5, result.Forecast.OneHourMm); Assert.Equal(25.5, result.Forecast.ThreeHoursMm); Assert.Equal(60, result.Forecast.SixHoursMm);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("duplicate")]
    [InlineData("gap")]
    [InlineData("units")]
    [InlineData("units-type")]
    [InlineData("root")]
    public async Task IncompleteAndInvalidWeatherIsNotTreatedAsZero(string invalid)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Payload(invalid)) }));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<WeatherDataException>(() => new OpenMeteoWeatherService(client).FetchAsync(23, 90, Hour));
    }

    [Fact]
    public async Task InvalidCoordinatesAndNonUtcAnchorMakeNoRequests()
    {
        using var handler = new Handler((_, _) => throw new Exception("Must not send")); using var client = new HttpClient(handler);
        var service = new OpenMeteoWeatherService(client);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.FetchAsync(double.NaN, 90, Hour));
        await Assert.ThrowsAsync<ArgumentException>(() => service.FetchAsync(23, 90, DateTime.SpecifyKind(Hour, DateTimeKind.Unspecified)));
        Assert.Equal(0, handler.Calls);
    }
}
