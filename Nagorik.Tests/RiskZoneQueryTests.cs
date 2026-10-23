using Microsoft.Extensions.Logging.Abstractions;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public class RiskZoneQueryTests
{
    [Fact]
    public async Task EmptyDatabaseReturnsFeatureCollection()
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        var result = await new RiskZoneQueryService(db, NullLogger<RiskZoneQueryService>.Instance).ListAsync();
        Assert.Equal("FeatureCollection", result.Type); Assert.Empty(result.Features); Assert.Equal(0, result.InvalidZoneCount);
    }

    [Fact]
    public async Task EmitsStandardCoordinatesSortedPropertiesAndUtcWhileSkippingInvalidRing()
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        const string valid = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}";
        db.Zones.AddRange(new Zone { Code = "Z2", Name = "Second", BoundaryGeoJson = valid },
            new Zone { Code = "Z1", Name = "<script>sample</script>", BoundaryGeoJson = valid, RiskLevel = ZoneRiskLevel.High, LastComputedAtUtc = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc) },
            new Zone { Code = "BAD", Name = "Invalid ring", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,24]]]}" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await new RiskZoneQueryService(db, NullLogger<RiskZoneQueryService>.Instance).ListAsync();
        Assert.Equal(1, result.InvalidZoneCount); Assert.Equal(2, result.Features.Count);
        var first = result.Features[0]; Assert.Equal("Feature", first.Type); Assert.Equal("Z1", first.Properties.Code);
        Assert.Equal("<script>sample</script>", first.Properties.Name); Assert.Equal("High", first.Properties.RiskLevel);
        Assert.Equal(DateTimeKind.Utc, first.Properties.LastComputedAtUtc!.Value.Kind);
        Assert.Equal(90, first.Geometry.GetProperty("coordinates")[0][0][0].GetDouble());
        Assert.Equal(23, first.Geometry.GetProperty("coordinates")[0][0][1].GetDouble());
        Assert.Null(result.Features[1].Properties.LastComputedAtUtc);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CancellationIsObserved()
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RiskZoneQueryService(db, NullLogger<RiskZoneQueryService>.Instance).ListAsync(new CancellationToken(true)));
    }
}
