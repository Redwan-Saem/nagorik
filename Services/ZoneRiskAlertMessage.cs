using System.Globalization;
using Nagorik.Api.Models;
namespace Nagorik.Api.Services;
public sealed record ZoneRiskAlertMessage(string Title,string Body,string Url)
{
    public static ZoneRiskAlertMessage Build(RiskHistory history)
    {
        ArgumentNullException.ThrowIfNull(history.Zone);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(history.ZoneId);
        var timestamp=DateTime.SpecifyKind(history.ComputedAtUtc,DateTimeKind.Utc)
            .ToString("yyyy-MM-dd HH:mm 'UTC'",CultureInfo.InvariantCulture);
        return new($"Area risk: {history.Zone.Name} — {history.Level}",
            $"Rule-based assessment for {history.Zone.Name} changed to {history.Level}. Evaluated {timestamp}. This experimental assessment uses rainfall and zone vulnerability. Open the map for details.",
            $"/map?zone={history.ZoneId.ToString(CultureInfo.InvariantCulture)}");
    }
}
