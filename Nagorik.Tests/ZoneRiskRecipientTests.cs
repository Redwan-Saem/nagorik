using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;
public class ZoneRiskRecipientTests
{
    [Fact]
    public async Task CanonicalChoiceOverridesLegacyAndGlobalOptOutAndRolesAreRespected()
    {
        var (db, connection) = TestDb.Create(); await using var d=db;await using var c=connection;
        var zone=RiskPredictionTests.Zone();db.Zones.Add(zone);await db.SaveChangesAsync();
        for(var id=1;id<=6;id++)
        {
            db.Users.Add(new User {Id=id,Role=id==5?"Crew":"Resident"});
            db.NotificationPreferences.Add(new(){UserId=id.ToString(),NotificationsEnabled=id!=3});
            if(id is 1 or 2 or 5 or 6)db.AreaSubscriptions.Add(new(){UserId=id.ToString(),ZoneId=zone.Code});
            if(id!=6)db.ResidentZoneSubscriptions.Add(new(){ResidentId=id,ZoneId=zone.Id,AlertsEnabled=id!=2});
        }
        await db.SaveChangesAsync();var provider=new ZoneRiskAlertRecipients(db);
        var recipients=await provider.ListAsync(zone.Id,zone.Code);
        Assert.Equal(new[]{"1","4","6"},recipients.OrderBy(x=>x).ToArray());
        var first=await db.ResidentZoneSubscriptions.SingleAsync(s=>s.ResidentId==1);first.AlertsEnabled=false;await db.SaveChangesAsync();
        Assert.DoesNotContain("1",await provider.ListAsync(zone.Id,zone.Code));
    }
}
