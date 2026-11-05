using System.Diagnostics;
using Google.OrTools.Sat;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed class BackupPowerSolver : IDisposable
{
    private static readonly Lazy<Task<bool>> NativeReady = new(() => Task.Run(() =>
    {
        // Warm this optional engine without delaying unrelated application pages.
        var warm = new CpModel(); var x = warm.NewBoolVar("startup"); warm.Maximize(x);
        // This one-variable startup check has no search deadline: loading the
        // native runtime on a busy host must not consume the request deadline.
        var engine = new CpSolver { StringParameters = "num_search_workers:1 cp_model_presolve:false" };
        if (engine.Solve(warm) != CpSolverStatus.Optimal) throw new InvalidOperationException("Backup-power solver startup check failed.");
        return true;
    }));
    public BackupPowerSolver() { _ = NativeReady.Value; }
    public static Task WarmUpAsync() => NativeReady.Value;
    // Bound CPU consumption of the public, stateless endpoint. No user data is kept.
    private readonly SemaphoreSlim gate = new(2, 2);
    public BackupPowerResult Solve(BackupPowerInput input, CancellationToken ct = default)
    {
        var errors = BackupPowerRules.Validate(input);
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors.Select(x => x.Key + ": " + x.Value)));
        if (!gate.Wait(0, ct)) return Empty("busy", "Two planners are already running. Retry shortly.");
        try { return SolveCore(input, ct); }
        finally { gate.Release(); }
    }

    private static BackupPowerResult Empty(string status, string message, string solver = "not_run") =>
        new(status, message, [], 0, 0, new Dictionary<string, int>(), false, solver);

    private static BackupPowerResult SolveCore(BackupPowerInput input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var shops = input.Shops!;
        var count = input.GridAvailable!.Length;
        var outageSlots = Enumerable.Range(0, count).Where(h => !input.GridAvailable![h]).ToArray();
        if (outageSlots.Length == 0 || shops.Count == 0)
        {
            var rows = outageSlots.Select(h => new BackupPowerHour(h, BackupPowerRules.Hour(input, h), [], [], 0, 0, 0)).ToArray();
            var empty = new BackupPowerResult("ok", "No generator allocation is needed.", rows, 0, 0,
                shops.ToDictionary(s => s.Id!, _ => 0), true, "optimal", 0)
            {
                DarkMinutes = input.SlotMinutes == null ? null : shops.ToDictionary(s => s.Id!, _ => 0)
            };
            BackupPowerRules.Verify(input, empty); return empty;
        }
        var capacity = input.GeneratorCapacityKw!.Value;
        // Use hundredths of a litre times 60 so minute-based fuel stays exact in CP-SAT.
        var budget = BackupPowerRules.Cents(input.FuelBudgetLitres!.Value) * 60;
        var mandatory = shops.Where(s => s.Type == "mandatory").ToArray();
        var mandatoryLoad = mandatory.Sum(s => s.Kw!.Value);
        if (mandatoryLoad > capacity)
            return Empty("infeasible", $"Mandatory shops [{string.Join(", ", mandatory.Select(s => s.Id))}] need {mandatoryLoad} kW in every outage; capacity is {capacity} kW.", "infeasible");

        if (!NativeReady.Value.IsCompletedSuccessfully)
            return Empty("not_solved", "The local optimization engine is starting or unavailable. Retry shortly; if this persists, check the server's native runtime installation. No infeasibility claim has been made.");

        var model = new CpModel();
        var on = new BoolVar[shops.Count, count];
        var objectiveVars = new List<IntVar>(); var objectiveCoefficients = new List<long>();
        for (var i = 0; i < shops.Count; i++)
        {
            for (var h = 0; h < count; h++)
            {
                on[i, h] = model.NewBoolVar($"on_{i}_{h}");
                if (input.GridAvailable![h] || shops[i].Type == "mandatory") model.Add(on[i, h] == 1);
                if (!input.GridAvailable[h]) { objectiveVars.Add(on[i, h]); objectiveCoefficients.Add(shops[i].ValuePerSlot![h]); }
            }
            if (shops[i].Type == "normal")
            {
                for (var start = 0; start < count; start++)
                {
                    var duration = 0;
                    for (var end = start; end < count && !input.GridAvailable[end]; end++)
                    {
                        duration += BackupPowerRules.Duration(input, end);
                        if (duration <= BackupPowerRules.DarkLimit(input)) continue;
                        model.Add(LinearExpr.Sum(Enumerable.Range(start, end - start + 1).Select(t => on[i, t])) >= 1);
                        break;
                    }
                }
            }
        }

        // At most 4,096 subsets. Only attainable loads need fuel-table tuples;
        // no capacity-sized array and no floating-point fuel arithmetic.
        var loads = new HashSet<int> { 0 };
        foreach (var shop in shops)
            foreach (var previous in loads.ToArray())
                if (previous + shop.Kw!.Value <= capacity) loads.Add(previous + shop.Kw.Value);
        var tuples = new long[loads.Count, 2]; var tuple = 0;
        foreach (var load in loads.Order()) { tuples[tuple, 0] = load; tuples[tuple++, 1] = BackupPowerRules.FuelCents(input, load); }
        var fuelVars = new List<IntVar>();
        foreach (var h in outageSlots)
        {
            var load = model.NewIntVar(0, capacity, $"load_{h}");
            var fuel = model.NewIntVar(0, input.FuelTable!.Max(b => BackupPowerRules.Cents(b.LitresPerHour!.Value)), $"fuel_{h}");
            model.Add(load == LinearExpr.WeightedSum(Enumerable.Range(0, shops.Count).Select(i => on[i, h]), shops.Select(s => (long)s.Kw!.Value)));
            model.AddAllowedAssignments(new IntVar[] { load, fuel }).AddTuples(tuples);
            fuelVars.Add(fuel);
        }
        model.Add(LinearExpr.WeightedSum(fuelVars, outageSlots.Select(h => (long)BackupPowerRules.Duration(input, h))) <= budget);
        model.Maximize(LinearExpr.WeightedSum(objectiveVars, objectiveCoefficients));
        var validation = model.Validate();
        if (validation.Length != 0) throw new InvalidOperationException("Invalid planner model: " + validation);
        var solver = new CpSolver { StringParameters = "max_time_in_seconds:6 num_search_workers:1 random_seed:0" };
        using var cancel = ct.Register(solver.StopSearch);
        var status = solver.Solve(model);
        ct.ThrowIfCancellationRequested();
        if (status == CpSolverStatus.Infeasible)
            return Empty("infeasible", $"No roster can jointly satisfy {capacity} kW capacity, {input.FuelBudgetLitres:0.00} L fuel, mandatory loads [{string.Join(", ", mandatory.Select(s => s.Id))}] and at most {BackupPowerRules.DarkLimit(input)} consecutive dark minutes across [{string.Join(", ", outageSlots.Select(h => BackupPowerRules.Hour(input, h)))}]. Revise these inputs; no unsafe roster is published.", "infeasible");
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            return Empty("not_solved", "The time limit expired before a feasible plan or infeasibility proof was found. This does not mean the input is infeasible.", status.ToString().ToLowerInvariant());

        var decisions = new bool[shops.Count, count];
        for (var i = 0; i < shops.Count; i++) for (var h = 0; h < count; h++) decisions[i, h] = solver.Value(on[i, h]) == 1;
        var hourly = new List<BackupPowerHour>();
        var darkHours = shops.ToDictionary(s => s.Id!, _ => 0);
        var totalFuel = fuelVars.Select((f, i) => solver.Value(f) * BackupPowerRules.Duration(input, outageSlots[i])).Sum();
        long cumulative = 0, value = 0;
        foreach (var h in outageSlots)
        {
            var active = Enumerable.Range(0, shops.Count).Where(i => decisions[i, h]).ToArray();
            var load = active.Sum(i => shops[i].Kw!.Value);
            var duration = BackupPowerRules.Duration(input, h);
            var fuel = BackupPowerRules.FuelCents(input, load) * duration; cumulative += fuel;
            value += active.Sum(i => (long)shops[i].ValuePerSlot![h]);
            var dark = new List<DarkPowerEntry>();
            for (var i = 0; i < shops.Count; i++) if (!decisions[i, h])
            {
                var shop = shops[i]; darkHours[shop.Id!] += duration;
                string reason;
                if (load + shop.Kw > capacity)
                    reason = $"Adding {shop.Id} ({shop.Kw} kW) to the selected {load} kW would exceed {capacity} kW capacity. Whole-day alternatives also preserve fuel and dark-run limits.";
                else
                {
                    var addedFuel = BackupPowerRules.FuelCents(input, load + shop.Kw!.Value) * duration - fuel;
                    reason = totalFuel + addedFuel > budget
                        ? $"Adding {shop.Id} to this slot would raise daily fuel from {totalFuel / 6000m:0.00} L to {(totalFuel + addedFuel) / 6000m:0.00} L, above {budget / 6000m:0.00} L."
                        : $"Whole-day selection: {shop.Id} offers {shop.ValuePerSlot![h]} value here. Current plan uses {totalFuel / 6000m:0.00}/{budget / 6000m:0.00} L with a {BackupPowerRules.DarkLimit(input)}-minute dark-run limit; alternative combinations compete across all outage hours.";
                }
                dark.Add(new(shop.Id!, reason));
            }
            hourly.Add(new(h, BackupPowerRules.Hour(input, h), active.Select(i => shops[i].Id!).ToArray(), dark, load, fuel / 6000m, cumulative / 6000m));
        }
        var optimal = status == CpSolverStatus.Optimal;
        var result = new BackupPowerResult("ok", optimal ? "Optimal whole-day roster proven for these inputs." : "A verified feasible roster was found within the time limit; optimality has not been proven.",
            hourly, value, totalFuel / 6000m, darkHours.ToDictionary(x => x.Key, x => x.Value / 60), optimal, optimal ? "optimal" : "feasible", (long)Math.Ceiling(solver.BestObjectiveBound))
        {
            DarkMinutes = input.SlotMinutes == null ? null : darkHours
        };
        BackupPowerRules.Verify(input, result);
        return result;
    }

    public void Dispose() => gate.Dispose();
}
