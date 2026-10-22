using System.Globalization;
using System.Text.Json;

namespace Nagorik.Api.Services;

public sealed record RainfallAmounts(double OneHourMm, double ThreeHoursMm, double SixHoursMm);
public sealed record WeatherRainfall(DateTime TimestampUtc, RainfallAmounts Past, RainfallAmounts Forecast);
public sealed class WeatherDataException(string message) : Exception(message);

public interface IWeatherService
{
    Task<WeatherRainfall> FetchAsync(double latitude, double longitude, DateTime hourUtc, CancellationToken ct = default);
}

public sealed class OpenMeteoWeatherService(HttpClient client) : IWeatherService
{
    public const string Source = "Open-Meteo";

    public async Task<WeatherRainfall> FetchAsync(double latitude, double longitude, DateTime hourUtc, CancellationToken ct = default)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Weather coordinates must be finite WGS84 coordinates.");
        if (hourUtc.Kind != DateTimeKind.Utc || hourUtc.Ticks % TimeSpan.TicksPerHour != 0)
            throw new ArgumentException("Supply a whole UTC hour.", nameof(hourUtc));
        var query = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&hourly=rain,showers&precipitation_unit=mm&timezone=UTC&timeformat=unixtime&start_hour={hourUtc.AddHours(-5):yyyy-MM-ddTHH:mm}&end_hour={hourUtc.AddHours(6):yyyy-MM-ddTHH:mm}");
        using var response = await client.GetAsync(query, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return Parse(document.RootElement, hourUtc);
    }

    private static WeatherRainfall Parse(JsonElement root, DateTime hourUtc)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("hourly_units", out var units) ||
            !UnitIs(units, "rain", "mm") || !UnitIs(units, "showers", "mm") || !UnitIs(units, "time", "unixtime") ||
            !root.TryGetProperty("hourly", out var hourly) || hourly.ValueKind != JsonValueKind.Object || !hourly.TryGetProperty("time", out var times) || times.ValueKind != JsonValueKind.Array ||
            !hourly.TryGetProperty("rain", out var rain) || rain.ValueKind != JsonValueKind.Array ||
            !hourly.TryGetProperty("showers", out var showers) || showers.ValueKind != JsonValueKind.Array ||
            times.GetArrayLength() != rain.GetArrayLength() || times.GetArrayLength() != showers.GetArrayLength())
            throw new WeatherDataException("Weather response has missing arrays or unexpected units.");
        var amounts = new Dictionary<long, double>();
        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            if (times[i].ValueKind != JsonValueKind.Number || !times[i].TryGetInt64(out var time) || time % 3600 != 0 || !Value(rain[i], out var a) || !Value(showers[i], out var b) ||
                !amounts.TryAdd(time, a + b)) throw new WeatherDataException("Weather response contains invalid, missing or duplicate hourly values.");
        }
        var anchor = new DateTimeOffset(hourUtc).ToUnixTimeSeconds();
        double Total(int start, int length)
        {
            var sum = 0d;
            for (var i = 0; i < length; i++)
            {
                if (!amounts.TryGetValue(anchor + (start + i) * 3600, out var value)) throw new WeatherDataException("Weather response does not cover the complete requested window.");
                sum += value;
            }
            if (!double.IsFinite(sum) || sum > 10000) throw new WeatherDataException("Weather accumulation is out of supported range.");
            return sum;
        }
        // Open-Meteo labels each rain/showers value by the END of its preceding hour.
        return new(hourUtc, new(Total(0, 1), Total(-2, 3), Total(-5, 6)), new(Total(1, 1), Total(1, 3), Total(1, 6)));
    }

    private static bool UnitIs(JsonElement units, string name, string expected) => units.ValueKind == JsonValueKind.Object &&
        units.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == expected;

    private static bool Value(JsonElement element, out double value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value) && value is >= 0 and <= 10000;
    }
}
