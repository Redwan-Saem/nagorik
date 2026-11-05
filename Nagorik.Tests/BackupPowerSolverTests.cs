using System.Diagnostics;
using System.Text.Json;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public sealed class BackupPowerSolverTests : IAsyncLifetime
{
    public Task InitializeAsync() => BackupPowerSolver.WarmUpAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public void OmittedDarkLimitDefaultsToTwoButExplicitNullIsInvalid()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Worked(), BackupPowerJson.Options))!.AsObject();
        node.Remove("max_consecutive_dark");
        var input = node.Deserialize<BackupPowerInput>(BackupPowerJson.Options)!;
        Assert.Equal(2, input.MaxConsecutiveDark); Assert.Empty(BackupPowerRules.Validate(input));
        node["max_consecutive_dark"] = null;
        input = node.Deserialize<BackupPowerInput>(BackupPowerJson.Options)!;
        Assert.Contains("max_consecutive_dark", BackupPowerRules.Validate(input).Keys);
    }

    public static BackupPowerInput Worked() => new()
    {
        GridAvailable = [true, true, false, false, false, true, true, false, false, false],
        GeneratorCapacityKw = 10, FuelBudgetLitres = 8m, MaxConsecutiveDark = 2,
        FuelTable = [new() { LoadKw = 4, LitresPerHour = .8m }, new() { LoadKw = 7, LitresPerHour = 1.2m }, new() { LoadKw = 10, LitresPerHour = 1.6m }],
        Shops = [
            new() { Id = "pharmacy", Kw = 3, Type = "mandatory", ValuePerSlot = [5,5,5,5,5,5,5,5,5,5] },
            new() { Id = "tailor", Kw = 4, Type = "normal", ValuePerSlot = [2,2,3,3,4,4,4,4,5,5] },
            new() { Id = "mobile", Kw = 3, Type = "normal", ValuePerSlot = [1,1,2,2,2,3,4,6,8,8] },
            new() { Id = "photocopy", Kw = 2, Type = "normal", ValuePerSlot = [3,3,5,5,4,3,2,2,1,1] },
            new() { Id = "tea_stall", Kw = 1, Type = "normal", ValuePerSlot = [2,2,2,3,3,2,2,3,3,3] }
        ]
    };

    [Fact]
    public void WorkedExampleProvesValue84AndFuel8()
    {
        using var solver = new BackupPowerSolver(); var input = Worked();
        var result = solver.Solve(input);
        Assert.Equal("ok", result.Status); Assert.True(result.OptimalityProven);
        Assert.Equal(84, result.ValueTotal); Assert.Equal(8m, result.FuelTotalLitres);
        Assert.Equal(6, result.Roster.Count); BackupPowerRules.Verify(input, result);
    }

    [Fact]
    public void CustomSlotsProrateFuelAndEnforceDarkLimitsInMinutes()
    {
        using var solver = new BackupPowerSolver();
        var input = new BackupPowerInput
        {
            StartMinute = 495,
            SlotMinutes = [30, 45, 15],
            GridAvailable = [false, false, false],
            GeneratorCapacityKw = 1,
            FuelBudgetLitres = .45m,
            FuelTable = [new() { LoadKw = 1, LitresPerHour = .60m }],
            MaxConsecutiveDarkMinutes = 30,
            Shops = [new() { Id = "shop", Kw = 1, Type = "normal", ValuePerSlot = [1, 2, 0] }]
        };

        var result = solver.Solve(input);
        Assert.Equal("ok", result.Status);
        Assert.Equal(.45m, result.FuelTotalLitres);
        Assert.Equal(45, result.DarkMinutes!["shop"]);
        Assert.Equal("08:45", result.Roster[1].Hour);
        Assert.Equal(["shop"], result.Roster[1].Powered);

        input.MaxConsecutiveDarkMinutes = 29;
        Assert.Equal("infeasible", solver.Solve(input).Status);
    }

    [Fact]
    public void CombinedLoadFuelIsRoundedUpOnceAndZeroLoadCostsNothing()
    {
        var input = Worked(); Assert.Equal(0, BackupPowerRules.FuelCents(input, 0));
        Assert.Equal(80, BackupPowerRules.FuelCents(input, 4));
        Assert.Equal(120, BackupPowerRules.FuelCents(input, 5));
        Assert.Equal(160, BackupPowerRules.FuelCents(input, 10));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void NoOutagesOrNoShopsAreValid(bool noOutages)
    {
        using var solver = new BackupPowerSolver(); var input = Worked();
        if (noOutages) input.GridAvailable = Enumerable.Repeat(true, 10).ToArray(); else input.Shops = [];
        var result = solver.Solve(input); Assert.Equal("ok", result.Status);
        Assert.Equal(0, result.ValueTotal); Assert.Equal(0, result.FuelTotalLitres);
        BackupPowerRules.Verify(input, result);
    }

    [Theory]
    [InlineData(2, 0)] [InlineData(1, 2)]
    public void MandatoryCapacityAndFuelConflictsAreInfeasible(int capacity, int fuel)
    {
        using var solver = new BackupPowerSolver(); var input = Worked();
        input.GeneratorCapacityKw = capacity; input.FuelBudgetLitres = fuel;
        input.FuelTable = [new() { LoadKw = capacity, LitresPerHour = .8m }];
        Assert.Empty(BackupPowerRules.Validate(input)); Assert.Equal("infeasible", solver.Solve(input).Status);
    }

    [Fact]
    public void FuelShortfallWithValidCapacityIsInfeasible()
    {
        using var solver = new BackupPowerSolver(); var input = Worked(); input.FuelBudgetLitres = 0;
        Assert.Equal("infeasible", solver.Solve(input).Status);
    }

    [Fact]
    public void GridHourResetsDarkRunAndOversizedNormalShopNeverUsesGenerator()
    {
        using var solver = new BackupPowerSolver(); var input = Worked();
        input.Shops = [new() { Id = "oversized", Kw = 20, Type = "normal", ValuePerSlot = new int[10] }];
        input.GridAvailable = [false, false, true, false, false, true, true, true, true, true];
        var result = solver.Solve(input); Assert.Equal("ok", result.Status); Assert.Equal(4, result.DarkHours["oversized"]);
        input.GridAvailable[2] = false; Assert.Equal("infeasible", solver.Solve(input).Status);
    }

    [Fact]
    public void ZeroDarkLimitRequiresEveryNormalShopOn()
    {
        using var solver = new BackupPowerSolver(); var input = Worked(); input.MaxConsecutiveDark = 0;
        Assert.Equal("infeasible", solver.Solve(input).Status);
        input.Shops = [input.Shops![0]]; Assert.Equal("ok", solver.Solve(input).Status);
    }

    [Fact]
    public void InvalidFieldsAreNamedAndCannotReachSolver()
    {
        var input = Worked(); input.GridAvailable = [true]; input.Shops![1].Id = input.Shops[0].Id;
        input.FuelBudgetLitres = .001m; input.FuelTable![0].LoadKw = 7;
        var errors = BackupPowerRules.Validate(input);
        Assert.Contains("grid_available", errors.Keys); Assert.Contains("shops[1].id", errors.Keys);
        Assert.Contains("fuel_budget_litres", errors.Keys); Assert.Contains("fuel_table[1].load_kw", errors.Keys);
        using var solver = new BackupPowerSolver(); Assert.Throws<ArgumentException>(() => solver.Solve(input));
    }

    [Fact]
    public void IndependentCheckerDetectsTamperedTotalsAndMissingPartitions()
    {
        using var solver = new BackupPowerSolver(); var input = Worked(); var result = solver.Solve(input);
        Assert.Equal("ok", result.Status);
        Assert.Throws<InvalidOperationException>(() => BackupPowerRules.Verify(input, result with { ValueTotal = result.ValueTotal + 1 }));
        Assert.Throws<InvalidOperationException>(() => BackupPowerRules.Verify(input, result with { Roster = result.Roster.Skip(1).ToArray() }));
        var rows = result.Roster.ToArray(); rows[0] = rows[0] with { FuelLitres = rows[0].FuelLitres + .001m };
        Assert.Throws<InvalidOperationException>(() => BackupPowerRules.Verify(input, result with { Roster = rows }));
    }

    [Fact]
    public void UpdatedOutageInputIsRecomputedWithoutReusingOldRoster()
    {
        using var solver = new BackupPowerSolver(); var input = Worked(); var previous = solver.Solve(input);
        input.GridAvailable![5] = false; var updated = solver.Solve(input);
        Assert.Equal(6, previous.Roster.Count); Assert.Equal("ok", updated.Status); Assert.Equal(7, updated.Roster.Count);
        Assert.Contains(updated.Roster, x => x.Slot == 5); BackupPowerRules.Verify(input, updated);
    }

    [Fact]
    public void SmallRandomCasesMatchAnIndependentExhaustiveOracle()
    {
        using var solver = new BackupPowerSolver(); var random = new Random(41);
        for (var sample = 0; sample < 20; sample++)
        {
            var input = new BackupPowerInput
            {
                GridAvailable = [true,true,false,false,false,true,true,true,true,true],
                GeneratorCapacityKw = 5, FuelBudgetLitres = random.Next(0, 8) / 2m, MaxConsecutiveDark = random.Next(0, 3),
                FuelTable = [new() { LoadKw = 2, LitresPerHour = .5m }, new() { LoadKw = 5, LitresPerHour = 1m }],
                Shops = Enumerable.Range(0, 3).Select(i => new BackupPowerShop { Id = "shop" + i, Kw = random.Next(1, 5), Type = i == 0 ? "mandatory" : "normal", ValuePerSlot = Enumerable.Range(0, 10).Select(_ => random.Next(0, 10)).ToArray() }).ToList()
            };
            var best = BruteForce(input); var result = solver.Solve(input);
            if (best is null) Assert.Equal("infeasible", result.Status);
            else { Assert.Equal("ok", result.Status); Assert.True(result.OptimalityProven); Assert.Equal(best.Value, result.ValueTotal); }
        }
    }

    private static long? BruteForce(BackupPowerInput input)
    {
        long? best = null;
        void Search(int hour, int[] dark, decimal fuel, long value)
        {
            if (hour == 10) { if (best is null || value > best) best = value; return; }
            if (input.GridAvailable![hour]) { Search(hour + 1, new int[dark.Length], fuel, value); return; }
            for (var mask = 0; mask < 1 << input.Shops!.Count; mask++)
            {
                var run = (int[])dark.Clone(); int load = 0; long added = 0; var valid = true;
                for (var i = 0; i < input.Shops.Count; i++)
                {
                    var shop = input.Shops[i];
                    if ((mask & (1 << i)) != 0) { load += shop.Kw!.Value; added += shop.ValuePerSlot![hour]; run[i] = 0; }
                    else if (shop.Type == "mandatory" || ++run[i] > input.MaxConsecutiveDark) valid = false;
                }
                if (!valid || load > input.GeneratorCapacityKw) continue;
                // Oracle computes its own lookup in decimal litres, not solver cents.
                var cost = load == 0 ? 0 : input.FuelTable!.First(x => x.LoadKw >= load).LitresPerHour!.Value;
                if (fuel + cost <= input.FuelBudgetLitres) Search(hour + 1, run, fuel + cost, value + added);
            }
        }
        Search(0, new int[input.Shops!.Count], 0, 0); return best;
    }

    [Fact]
    public void TwelveShopDayReturnsVerifiedPlanWithinBudgetedTime()
    {
        using var solver = new BackupPowerSolver(); var input = Worked();
        input.GeneratorCapacityKw = 20; input.FuelBudgetLitres = 17;
        input.GridAvailable = Enumerable.Repeat(false, 10).ToArray();
        input.FuelTable = [new() { LoadKw = 5, LitresPerHour = .8m }, new() { LoadKw = 10, LitresPerHour = 1.2m }, new() { LoadKw = 15, LitresPerHour = 1.7m }, new() { LoadKw = 20, LitresPerHour = 2.2m }];
        input.Shops = Enumerable.Range(0, 12).Select(i => new BackupPowerShop { Id = "shop" + i, Kw = 1 + i % 5, Type = i < 2 ? "mandatory" : "normal", ValuePerSlot = Enumerable.Range(0,10).Select(h => (i * 3 + h) % 10).ToArray() }).ToList();
        var watch = Stopwatch.StartNew(); var result = solver.Solve(input);
        Assert.Equal("ok", result.Status); BackupPowerRules.Verify(input, result); Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
    }
}
