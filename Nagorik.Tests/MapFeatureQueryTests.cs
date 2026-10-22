using Nagorik.Api.Services;
using Nagorik.Api.Models;

namespace Nagorik.Tests;

public class MapFeatureQueryTests
{
    [Fact]
    public async Task EmptyDrainageCollectionAndCancellationAreExplicit()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var service = new MapFeatureQueryService(db);
        var result = await service.DrainageAsync();
        Assert.Equal("FeatureCollection", result.Type); Assert.Empty(result.Features); Assert.Equal(0, result.OmittedCount);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DrainageAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task DrainageReturnsAllStatusesInStableIdOrderWithoutTracking()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        var zone = new Zone { Code = "Z1", Name = "Zone", BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" };
        foreach (var status in Enum.GetValues<DrainageStatus>()) db.DrainagePoints.Add(new DrainagePoint { Zone = zone, Latitude = 23.74, Longitude = 90.37, Status = status });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await new MapFeatureQueryService(db).DrainageAsync();
        Assert.Equal(new[] { "Unknown", "Clear", "Blocked" }, result.Features.Select(f => f.Properties.Status));
        Assert.Equal(result.Features.Select(f => f.Id).Order(), result.Features.Select(f => f.Id));
        Assert.All(result.Features, f => Assert.Equal(new[] { 90.37, 23.74 }, f.Geometry.Coordinates));
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
