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

    [Theory]
    [InlineData("/api/report-photos/11111111111141118111111111111111.png", true)]
    [InlineData("/uploads/reports/11111111111141118111111111111111.jpg", true)]
    [InlineData("https://external.invalid/private.jpg", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("//external.invalid/x.png", false)]
    [InlineData("/uploads/reports/../../secret.png", false)]
    public async Task ReportsProjectSafePhotosStableReferencesAndExcludeUnlocatedRecords(string photo, bool accepted)
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        TestDb.AddUser(db);
        db.Reports.AddRange(new Report { UserId = 1, Title = "<img onerror=evil>", Location = "Campus", Latitude = 23.74,
            Longitude = 90.37, ReferenceNo = "UA-000001", ImageUrl = photo, Status = ReportStatus.InProgress },
            new Report { UserId = 1, Title = "Address-only", Latitude = 0, Longitude = 0 },
            new Report { UserId = 1, Title = "Bad coordinates", Latitude = 200, Longitude = 90 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await new MapFeatureQueryService(db).ReportsAsync();
        var report = Assert.Single(result.Features);
        Assert.Equal(new[] { 90.37, 23.74 }, report.Geometry.Coordinates);
        Assert.Equal("UA-000001", report.Properties.Reference); Assert.Equal("InProgress", report.Properties.Status);
        Assert.Equal(accepted ? photo : null, report.Properties.PhotoUrl);
        Assert.Equal("<img onerror=evil>", report.Properties.Title);
        Assert.Equal(2, result.OmittedCount); Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ReportsWithSameCreationTimeUseDescendingIdAndLegacyReference()
    {
        var (db, connection) = TestDb.Create(); await using var d = db; await using var c = connection;
        TestDb.AddUser(db);
        var time = DateTime.UtcNow;
        db.Reports.AddRange(new Report { UserId = 1, Latitude = 23, Longitude = 90, CreatedAt = time },
            new Report { UserId = 1, Latitude = 23, Longitude = 90, CreatedAt = time });
        await db.SaveChangesAsync();
        var result = await new MapFeatureQueryService(db).ReportsAsync();
        Assert.True(result.Features[0].Id > result.Features[1].Id);
        Assert.Equal($"R-{result.Features[0].Id:D6}", result.Features[0].Properties.Reference);
        Assert.Equal(DateTimeKind.Utc, result.Features[0].Properties.CreatedAtUtc.Kind);
    }
}
