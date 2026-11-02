using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
public class BusOccupancyTests
{
    sealed class Clock:TimeProvider{public DateTimeOffset Now=new(2026,10,23,10,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
    [Fact]public async Task CooldownIsAtomicFreshnessExpiresAndUpdatesDoNotCreateDuplicateVotes()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await TransportSearchTests.Seed(db);var user=TestDb.AddUser(db);var clock=new Clock();var s=new BusOccupancyService(db,clock);
        var id=await db.TransportLinks.Where(x=>x.Mode=="Bus").Select(x=>x.Id).FirstAsync();Assert.Equal("Unknown",(await s.ReadAsync(id)).Status);
        await s.SubmitAsync(user.Id,id,"Full");Assert.Equal("Full",(await s.ReadAsync(id)).Level);
        Assert.Equal(429,(await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(user.Id,id,"Standing"))).Status);
        clock.Now=clock.Now.AddMinutes(5);await s.SubmitAsync(user.Id,id,"SeatsAvailable");Assert.Single(db.BusOccupancyReports);Assert.Equal(1,(await s.ReadAsync(id)).Reports);
        clock.Now=clock.Now.AddMinutes(15);Assert.Equal("Unknown",(await s.ReadAsync(id)).Status);
    }
    [Fact]public async Task OnlyRealResidentsAndEnabledBusSegmentsCanBeReported()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await TransportSearchTests.Seed(db);var user=TestDb.AddUser(db);user.Role="Dispatcher";await db.SaveChangesAsync();var s=new BusOccupancyService(db,new Clock());
        Assert.Equal(403,(await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(user.Id,1,"Full"))).Status);
        user.Role="Resident";await db.SaveChangesAsync();Assert.Equal(400,(await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(user.Id,1,"Empty"))).Status);
        var cng=await db.TransportLinks.FirstAsync(x=>x.Mode=="CNG");Assert.Equal(404,(await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(user.Id,cng.Id,"Full"))).Status);
    }
}
