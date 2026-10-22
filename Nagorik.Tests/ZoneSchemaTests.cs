using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
using Nagorik.Api.Models;

namespace Nagorik.Tests;

public sealed class ZoneSchemaTests
{
    private const string Polygon = "{\"type\":\"Polygon\",\"coordinates\":[[[90.37,23.74],[90.38,23.74],[90.38,23.75],[90.37,23.74]]]}";
    private const string PreviousMigration = "20261017030335_AddUtilityRoutingRules";

    private static AppDbContext Context(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static Zone Sample(string code = "Z1") => new()
    {
        Code = code, Name = "Dhanmondi", BoundaryGeoJson = Polygon
    };

    [Fact]
    public async Task MigrationRoundTripsGeometryAndNullableComputationThenRiskUpdate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        await db.Database.MigrateAsync();
        db.Zones.Add(Sample());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var saved = await db.Zones.SingleAsync();
        Assert.Equal(Polygon, saved.BoundaryGeoJson);
        Assert.Equal(ZoneRiskLevel.Unknown, saved.RiskLevel);
        Assert.Null(saved.LastComputedAtUtc);

        var computed = new DateTime(2026, 10, 22, 8, 30, 0, DateTimeKind.Utc);
        saved.RiskLevel = ZoneRiskLevel.Severe;
        saved.LastComputedAtUtc = computed;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        saved = await db.Zones.SingleAsync();
        Assert.Equal(ZoneRiskLevel.Severe, saved.RiskLevel);
        Assert.Equal(computed, saved.LastComputedAtUtc);
    }

    [Fact]
    public async Task ZoneCodesAreUniqueIgnoringCase()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        db.Zones.Add(Sample("Z1"));
        await db.SaveChangesAsync();
        db.Zones.Add(Sample("z1"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(ZoneRiskLevel.Unknown)]
    [InlineData(ZoneRiskLevel.Low)]
    [InlineData(ZoneRiskLevel.Medium)]
    [InlineData(ZoneRiskLevel.High)]
    [InlineData(ZoneRiskLevel.Severe)]
    public async Task DefinedRiskLevelsRoundTrip(ZoneRiskLevel level)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var zone = Sample(); zone.RiskLevel = level;
        db.Zones.Add(zone); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(level, (await db.Zones.SingleAsync()).RiskLevel);
    }

    [Theory]
    [InlineData("code-empty")]
    [InlineData("code-padding")]
    [InlineData("code-length")]
    [InlineData("name-blank")]
    [InlineData("name-length")]
    [InlineData("risk")]
    [InlineData("json")]
    [InlineData("leaflet-array")]
    [InlineData("point")]
    [InlineData("missing-type")]
    [InlineData("missing-coordinates")]
    [InlineData("empty-coordinates")]
    public async Task DatabaseRejectsInvalidFields(string invalid)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var zone = Sample();
        switch (invalid)
        {
            case "code-empty": zone.Code = ""; break;
            case "code-padding": zone.Code = " Z1 "; break;
            case "code-length": zone.Code = new string('z', 41); break;
            case "name-blank": zone.Name = "   "; break;
            case "name-length": zone.Name = new string('n', 101); break;
            case "risk": zone.RiskLevel = (ZoneRiskLevel)999; break;
            case "json": zone.BoundaryGeoJson = "not JSON"; break;
            case "leaflet-array": zone.BoundaryGeoJson = "[[23.74,90.37],[23.75,90.38]]"; break;
            case "point": zone.BoundaryGeoJson = "{\"type\":\"Point\",\"coordinates\":[90.37,23.74]}"; break;
            case "missing-type": zone.BoundaryGeoJson = "{\"coordinates\":[[]]}"; break;
            case "missing-coordinates": zone.BoundaryGeoJson = "{\"type\":\"Polygon\"}"; break;
            case "empty-coordinates": zone.BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[]}"; break;
        }
        db.Zones.Add(zone);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task MultiPolygonIsStoredWithoutReorderingLongitudeLatitude()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var zone = Sample();
        zone.BoundaryGeoJson = "{\"type\":\"MultiPolygon\",\"coordinates\":[[[[90.37,23.74],[90.38,23.74],[90.38,23.75],[90.37,23.74]]]]}";
        db.Zones.Add(zone); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(zone.BoundaryGeoJson, (await db.Zones.SingleAsync()).BoundaryGeoJson);
    }

    [Fact]
    public async Task UpgradeAndRollbackPreserveLegacyRiskAndMapData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Context(connection);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        db.WaterloggingRisks.Add(new WaterloggingRisk { ZoneId = "Z1", ZoneName = "Legacy Dhanmondi", RiskLevel = "High", LastUpdated = DateTime.UtcNow });
        db.ZoneMapData.Add(new ZoneMapData { ZoneId = "Z1", RiskLevel = "High", BoundaryGeoJson = "[[23.74,90.37]]", DrainagePumpStatus = "Working" });
        await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        Assert.Empty(await db.Zones.ToListAsync());
        db.Zones.Add(Sample()); await db.SaveChangesAsync();
        await migrator.MigrateAsync(PreviousMigration);
        db.ChangeTracker.Clear();
        Assert.Equal("Legacy Dhanmondi", (await db.WaterloggingRisks.SingleAsync()).ZoneName);
        Assert.Equal("[[23.74,90.37]]", (await db.ZoneMapData.SingleAsync()).BoundaryGeoJson);
        await migrator.MigrateAsync();
        Assert.Empty(await db.Zones.ToListAsync());
        Assert.Single(await db.WaterloggingRisks.ToListAsync());
    }
}
