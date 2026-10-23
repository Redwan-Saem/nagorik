using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Tests;
public class CrewLocationSchemaTests
{
    [Theory]
    [InlineData(91d, 90d, true)]
    [InlineData(23d, 181d, true)]
    [InlineData(23d, null, true)]
    [InlineData(23d, 90d, false)]
    public async Task RejectsInvalidOrIncompleteLocation(double? latitude, double? longitude, bool timestamp)
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        db.Crews.Add(new Crew { Name = "Crew", User = new User { Email = "crew@schema.invalid" },
            LastLatitude = latitude, LastLongitude = longitude, LastSeenAtUtc = timestamp ? DateTime.UtcNow : null });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task ExistingCrewMayHaveUnknownLocation()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        db.Crews.Add(new Crew { Name = "Crew", User = new User { Email = "crew@schema.invalid" } });
        await db.SaveChangesAsync(); Assert.Null((await db.Crews.SingleAsync()).LastSeenAtUtc);
    }
}
