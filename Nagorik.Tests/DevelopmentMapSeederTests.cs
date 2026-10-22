using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Nagorik.Api.Data;
using Nagorik.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Nagorik.Tests;

public class DevelopmentMapSeederTests
{
    [Theory]
    [InlineData("Production", true, 0)]
    [InlineData("Development", false, 0)]
    [InlineData("Development", true, 3)]
    public async Task SamplesAreDevelopmentOnlyExplicitlyDisableableAndRepeatable(string environment, bool enabled, int count)
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        var env = new Mock<IHostEnvironment>(); env.SetupGet(x => x.EnvironmentName).Returns(environment);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["DevelopmentData:SeedMapSamples"] = enabled.ToString() }).Build();
        await DevelopmentMapSeeder.SeedAsync(db, env.Object, config);
        await DevelopmentMapSeeder.SeedAsync(db, env.Object, config);
        Assert.Equal(count, await db.Zones.CountAsync());
        Assert.Equal(count * 2, await db.DrainagePoints.CountAsync());
        foreach (var zone in await db.Zones.ToListAsync())
        {
            Assert.Contains("sample boundary", zone.Name); Assert.Null(zone.LastComputedAtUtc);
            using var geometry = JsonDocument.Parse(zone.BoundaryGeoJson);
            var ring = geometry.RootElement.GetProperty("coordinates")[0];
            Assert.Equal(5, ring.GetArrayLength());
            Assert.Equal(ring[0].GetRawText(), ring[4].GetRawText());
            Assert.InRange(ring[0][0].GetDouble(), 90, 91); // Longitude first.
            Assert.InRange(ring[0][1].GetDouble(), 23, 24);
        }
    }

    [Fact]
    public async Task ExistingZonesAndDrainageAreNotReplacedBySampleData()
    {
        var (db, connection) = TestDb.Create();
        await using var d = db; await using var c = connection;
        db.Zones.Add(new Zone { Code = "z1", Name = "Verified area", RiskLevel = ZoneRiskLevel.Severe,
            BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" });
        await db.SaveChangesAsync();
        var env = new Mock<IHostEnvironment>(); env.SetupGet(x => x.EnvironmentName).Returns("Development");
        await DevelopmentMapSeeder.SeedAsync(db, env.Object, new ConfigurationBuilder().Build());
        var preserved = await db.Zones.SingleAsync(z => z.Code == "Z1");
        Assert.Equal("Verified area", preserved.Name); Assert.Equal(ZoneRiskLevel.Severe, preserved.RiskLevel);
        Assert.False(await db.DrainagePoints.AnyAsync(p => p.ZoneId == preserved.Id));
        Assert.Equal(4, await db.DrainagePoints.CountAsync());
    }
}
