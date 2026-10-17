using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class UtilityRoutingServiceTests
{
    [Theory]
    [InlineData(AnomalyType.WaterLeak, UtilityProvider.WaterAuthority)]
    [InlineData(AnomalyType.GasLeak, UtilityProvider.GasAuthority)]
    public async Task SeededGenericRoutesUseExplicitProviderIdentifiers(AnomalyType type, UtilityProvider provider)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var result = await new UtilityRoutingService(db).ResolveAsync(type, 1);
        Assert.Equal(UtilityRoutingOutcome.Matched, result.Outcome);
        Assert.Equal(provider, result.Utility); Assert.NotNull(result.RuleId);
        Assert.Single(result.MatchingRuleIds);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Empty(await db.Reports.ToListAsync()); Assert.Empty(await db.NotificationLogs.ToListAsync());
    }

    [Theory]
    [InlineData(AnomalyType.ExposedWire)]
    [InlineData(AnomalyType.FallenPole)]
    [InlineData(AnomalyType.PowerOutage)]
    [InlineData(AnomalyType.Other)]
    public async Task UnverifiedElectricityTemplatesAndUnknownTypesDoNotGuessProviders(AnomalyType type)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var result = await new UtilityRoutingService(db).ResolveAsync(type, 1, 23.81, 90.41);
        Assert.Equal(UtilityRoutingOutcome.NoMatch, result.Outcome);
        Assert.Equal(UtilityProvider.Unassigned, result.Utility);
        Assert.Null(result.RuleId); Assert.Empty(result.MatchingRuleIds);
    }

    [Fact]
    public async Task ActivatingVerifiedAreaTemplateChangesRoutingWithoutCodeOrRestart()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var routing = new UtilityRoutingService(db);
        var template = await db.UtilityRoutingRules.AsNoTracking().SingleAsync(r =>
            r.Utility == UtilityProvider.Desco && r.AreaId == 1 && r.AnomalyType == AnomalyType.PowerOutage);
        await db.UtilityRoutingRules.Where(r => r.Id == template.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true));
        var matched = await routing.ResolveAsync(AnomalyType.PowerOutage, 1);
        Assert.Equal(UtilityProvider.Desco, matched.Utility); Assert.Equal(template.Id, matched.RuleId);
        Assert.Equal(UtilityRoutingOutcome.NoMatch, (await routing.ResolveAsync(AnomalyType.PowerOutage, 2)).Outcome);
        await db.UtilityRoutingRules.Where(r => r.Id == template.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
        Assert.Equal(UtilityRoutingOutcome.NoMatch, (await routing.ResolveAsync(AnomalyType.PowerOutage, 1)).Outcome);
    }

    [Theory]
    [InlineData(20, 90, true)]
    [InlineData(30, 100, true)]
    [InlineData(23.8, 95, true)]
    [InlineData(19.99, 95, false)]
    [InlineData(30.01, 95, false)]
    [InlineData(25, 89.99, false)]
    [InlineData(25, 100.01, false)]
    public async Task GeographicBoundsIncludeEdgesAndRequireBothAreaAndType(double lat, double lon, bool matches)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        await DisableDefaults(db);
        var rule = Rule(UtilityProvider.Dpdc, area: 2); Bounds(rule);
        db.UtilityRoutingRules.Add(rule); await db.SaveChangesAsync();
        var routing = new UtilityRoutingService(db);
        var result = await routing.ResolveAsync(AnomalyType.PowerOutage, 2, lat, lon);
        Assert.Equal(matches ? UtilityRoutingOutcome.Matched : UtilityRoutingOutcome.NoMatch, result.Outcome);
        Assert.Equal(UtilityRoutingOutcome.NoMatch, (await routing.ResolveAsync(AnomalyType.Other, 2, lat, lon)).Outcome);
        Assert.Equal(UtilityRoutingOutcome.NoMatch, (await routing.ResolveAsync(AnomalyType.PowerOutage, 1, lat, lon)).Outcome);
        Assert.Equal(UtilityRoutingOutcome.NoMatch, (await routing.ResolveAsync(AnomalyType.PowerOutage, 2)).Outcome);
    }

    [Fact]
    public async Task PriorityOverridesSpecificityAndInactiveHigherPriorityRulesAreIgnored()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        await DisableDefaults(db);
        var specific = Rule(UtilityProvider.Desco, 10, 1); Bounds(specific);
        var broad = Rule(UtilityProvider.Dpdc, 20);
        var inactive = Rule(UtilityProvider.Desco, 30); inactive.IsActive = false;
        db.UtilityRoutingRules.AddRange(specific, broad, inactive); await db.SaveChangesAsync();
        var result = await new UtilityRoutingService(db).ResolveAsync(AnomalyType.PowerOutage, 1, 25, 95);
        Assert.Equal(UtilityProvider.Dpdc, result.Utility); Assert.Equal(broad.Id, result.RuleId);
    }

    [Fact]
    public async Task EqualPriorityRanksAreaThenBoundsThenExactAnomalyType()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        await DisableDefaults(db);
        var global = Rule(UtilityProvider.Dpdc); global.AnomalyType = null;
        var boundsOnly = Rule(UtilityProvider.Dpdc); Bounds(boundsOnly);
        var areaOnly = Rule(UtilityProvider.Desco, area: 1); areaOnly.AnomalyType = null;
        var areaAndBounds = Rule(UtilityProvider.Dpdc, area: 1); Bounds(areaAndBounds); areaAndBounds.AnomalyType = null;
        var exact = Rule(UtilityProvider.Desco, area: 1); Bounds(exact);
        db.UtilityRoutingRules.AddRange(global, boundsOnly, areaOnly, areaAndBounds, exact);
        await db.SaveChangesAsync();
        var routing = new UtilityRoutingService(db);
        var result = await routing.ResolveAsync(AnomalyType.PowerOutage, 1, 25, 95);
        Assert.Equal(exact.Id, result.RuleId);
        await db.UtilityRoutingRules.Where(r => r.Id == exact.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
        Assert.Equal(areaAndBounds.Id, (await routing.ResolveAsync(AnomalyType.PowerOutage, 1, 25, 95)).RuleId);
        await db.UtilityRoutingRules.Where(r => r.Id == areaAndBounds.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
        Assert.Equal(areaOnly.Id, (await routing.ResolveAsync(AnomalyType.PowerOutage, 1, 25, 95)).RuleId);
        Assert.Equal(boundsOnly.Id, (await routing.ResolveAsync(AnomalyType.PowerOutage, null, 25, 95)).RuleId);
        Assert.Equal(global.Id, (await routing.ResolveAsync(AnomalyType.Other)).RuleId);
    }

    [Fact]
    public async Task EquallyRankedConflictingProvidersReturnAmbiguityUntilConfigurationChanges()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        await DisableDefaults(db);
        var desco = Rule(UtilityProvider.Desco, 100, 1); var dpdc = Rule(UtilityProvider.Dpdc, 100, 1);
        db.UtilityRoutingRules.AddRange(desco, dpdc); await db.SaveChangesAsync();
        var routing = new UtilityRoutingService(db);
        var result = await routing.ResolveAsync(AnomalyType.PowerOutage, 1);
        Assert.Equal(UtilityRoutingOutcome.Ambiguous, result.Outcome);
        Assert.Equal(UtilityProvider.Unassigned, result.Utility); Assert.Null(result.RuleId);
        Assert.Equal(new[] { desco.Id, dpdc.Id }.Order(), result.MatchingRuleIds);
        await db.UtilityRoutingRules.Where(r => r.Id == dpdc.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.Priority, 101));
        Assert.Equal(dpdc.Id, (await routing.ResolveAsync(AnomalyType.PowerOutage, 1)).RuleId);
        await db.UtilityRoutingRules.Where(r => r.Id == dpdc.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
        Assert.Equal(desco.Id, (await routing.ResolveAsync(AnomalyType.PowerOutage, 1)).RuleId);
    }

    [Fact]
    public async Task SameProviderTiesChooseLowestIdAndExposeAllTopRankMatches()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        await DisableDefaults(db);
        var first = Rule(UtilityProvider.Desco); var second = Rule(UtilityProvider.Desco);
        db.UtilityRoutingRules.AddRange(first, second); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await new UtilityRoutingService(db).ResolveAsync(AnomalyType.PowerOutage);
        Assert.Equal(UtilityRoutingOutcome.Matched, result.Outcome);
        Assert.Equal(Math.Min(first.Id, second.Id), result.RuleId);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), result.MatchingRuleIds);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("type")]
    [InlineData("zero-area")]
    [InlineData("negative-area")]
    [InlineData("lat-only")]
    [InlineData("lon-only")]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("lat-range")]
    [InlineData("lon-range")]
    public async Task InvalidInputReturnsExplicitOutcomeBeforeDatabaseAccess(string invalid)
    {
        // There are no tables: any attempted database query would fail this test.
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        var type = invalid == "type" ? (AnomalyType)999 : AnomalyType.PowerOutage;
        int? area = invalid == "zero-area" ? 0 : invalid == "negative-area" ? -1 : null;
        double? lat = invalid switch { "lat-only" => 23.8, "nan" => double.NaN, "infinity" => double.PositiveInfinity, "lat-range" => 91, "lon-range" => 23.8, _ => null };
        double? lon = invalid switch { "lon-only" => 90.4, "nan" or "infinity" or "lat-range" => 90.4, "lon-range" => -181, _ => null };
        var result = await new UtilityRoutingService(db).ResolveAsync(type, area, lat, lon);
        Assert.Equal(UtilityRoutingOutcome.InvalidInput, result.Outcome);
        Assert.Equal(UtilityProvider.Unassigned, result.Utility); Assert.Empty(result.MatchingRuleIds);
    }

    [Fact]
    public async Task UnknownAreaIsInvalidAndCancellationIsObserved()
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        var routing = new UtilityRoutingService(db);
        Assert.Equal(UtilityRoutingOutcome.InvalidInput, (await routing.ResolveAsync(AnomalyType.WaterLeak, 999)).Outcome);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            routing.ResolveAsync(AnomalyType.WaterLeak, ct: new CancellationToken(true)));
    }

    private static Task<int> DisableDefaults(AppDbContext db) =>
        db.UtilityRoutingRules.ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));

    private static UtilityRoutingRule Rule(UtilityProvider utility, int priority = 10, int? area = null) => new()
    {
        Name = "Synthetic rule " + Guid.NewGuid().ToString("N"), Utility = utility,
        AnomalyType = AnomalyType.PowerOutage, AreaId = area, Priority = priority, IsActive = true
    };

    private static void Bounds(UtilityRoutingRule rule)
    {
        rule.MinLatitude = 20; rule.MaxLatitude = 30; rule.MinLongitude = 90; rule.MaxLongitude = 100;
    }
}
