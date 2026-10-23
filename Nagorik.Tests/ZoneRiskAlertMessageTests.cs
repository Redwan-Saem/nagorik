using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Tests;
public class ZoneRiskAlertMessageTests
{
    [Fact]public void MessageNamesZoneRuleRiskAndEvaluationAndLinksOnlyToFocusedPublicMap()
    {
        var message=ZoneRiskAlertMessage.Build(new(){ZoneId=7,Zone=new(){Name="Dhanmondi"},Level=ZoneRiskLevel.Severe,
            ComputedAtUtc=new DateTime(2026,10,23,12,30,0,DateTimeKind.Utc)});
        Assert.Contains("Dhanmondi",message.Title);Assert.Contains("Severe",message.Body);Assert.Contains("Rule-based",message.Body);
        Assert.Contains("2026-10-23 12:30 UTC",message.Body);Assert.Equal("/map?zone=7",message.Url);
        Assert.DoesNotContain("/dispatcher",message.Url);Assert.DoesNotContain("ML",message.Body);
    }
    [Fact]public void InvalidZoneCannotCreateAnAmbiguousNotificationLink()=>
        Assert.Throws<ArgumentOutOfRangeException>(()=>ZoneRiskAlertMessage.Build(new(){ZoneId=0,Zone=new()}));
}
