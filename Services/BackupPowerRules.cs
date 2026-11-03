using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public static class BackupPowerRules
{
    public const int Slots = 10;
    public static string Hour(int slot) => $"{slot + 10:00}:00";
    public static long Cents(decimal litres) => checked((long)(litres * 100));

    public static Dictionary<string, string> Validate(BackupPowerInput? input)
    {
        var errors = new Dictionary<string, string>();
        if (input is null) { errors["body"] = "Supply a JSON object."; return errors; }
        if (input.GridAvailable is not { Length: Slots }) errors["grid_available"] = "Exactly 10 booleans are required, for 10:00 through 19:00.";
        if (input.GeneratorCapacityKw is not (>= 1 and <= 1_000_000)) errors["generator_capacity_kw"] = "Use a whole number from 1 to 1,000,000 kW.";
        if (input.FuelBudgetLitres is not { } budget || !ValidLitres(budget)) errors["fuel_budget_litres"] = "Use 0 to 1,000,000 litres with at most two decimal places.";
        if (input.MaxConsecutiveDark is not (>= 0 and <= Slots)) errors["max_consecutive_dark"] = "Use a whole number from 0 to 10.";
        if (input.FuelTable is not { Count: >= 1 and <= 1000 } table || table.Any(x => x is null))
            errors["fuel_table"] = "Supply 1 to 1,000 non-null fuel bands.";
        else
        {
            var previous = 0;
            for (var i = 0; i < table.Count; i++)
            {
                var band = table[i];
                if (band.LoadKw is not { } load || load <= previous || load > 1_000_000)
                    errors[$"fuel_table[{i}].load_kw"] = "Load levels must be positive, strictly increasing whole kW.";
                else previous = load;
                if (band.LitresPerHour is not { } fuel || !ValidLitres(fuel))
                    errors[$"fuel_table[{i}].litres_per_hour"] = "Use 0 to 1,000,000 litres with at most two decimal places.";
            }
            if (table[^1].LoadKw != input.GeneratorCapacityKw) errors["fuel_table"] = "The final load level must equal generator_capacity_kw.";
        }
        if (input.Shops is not { Count: <= 12 } shops || shops.Any(x => x is null)) errors["shops"] = "Supply an array of at most 12 non-null shops; an empty array is allowed.";
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < shops.Count; i++)
            {
                var shop = shops[i];
                if (string.IsNullOrWhiteSpace(shop.Id) || shop.Id.Length > 80 || shop.Id != shop.Id.Trim() || !ids.Add(shop.Id))
                    errors[$"shops[{i}].id"] = "Use a unique, nonblank ID of at most 80 characters without surrounding whitespace.";
                if (shop.Kw is not (>= 1 and <= 1_000_000)) errors[$"shops[{i}].kw"] = "Use a positive whole-number demand up to 1,000,000 kW.";
                if (shop.Type is not ("mandatory" or "normal")) errors[$"shops[{i}].type"] = "Use mandatory or normal.";
                if (shop.ValuePerSlot is not { Length: Slots } values || values.Any(x => x < 0 || x > 1_000_000))
                    errors[$"shops[{i}].value_per_slot"] = "Exactly 10 whole-number values from 0 to 1,000,000 are required.";
            }
        }
        return errors;
    }

    private static bool ValidLitres(decimal value) => value is >= 0 and <= 1_000_000 && decimal.Truncate(value * 100) == value * 100;
    public static long FuelCents(BackupPowerInput input, int load) => load == 0 ? 0 : Cents(input.FuelTable!.First(b => b.LoadKw >= load).LitresPerHour!.Value);

    // Independent recomputation: do not trust a solver's totals or inferred safety.
    public static void Verify(BackupPowerInput input, BackupPowerResult result)
    {
        if (result.Status != "ok") throw new InvalidOperationException("Only a feasible roster can be verified.");
        var shops = input.Shops!;
        var grid = input.GridAvailable!;
        var rows = result.Roster.ToDictionary(x => x.Slot);
        var runs = shops.ToDictionary(x => x.Id!, _ => 0);
        var darkHours = shops.ToDictionary(x => x.Id!, _ => 0);
        long fuel = 0, value = 0;
        if (rows.Count != grid.Count(x => !x)) throw new InvalidOperationException("Missing/extra outage hours.");
        for (var slot = 0; slot < Slots; slot++)
        {
            if (grid[slot])
            {
                if (rows.ContainsKey(slot)) throw new InvalidOperationException("Grid hour must not consume generator fuel.");
                foreach (var shop in shops) runs[shop.Id!] = 0;
                continue;
            }
            if (!rows.TryGetValue(slot, out var row) || row.Hour != Hour(slot)) throw new InvalidOperationException("Missing outage slot.");
            var partition = row.Powered.Concat(row.Dark.Select(x => x.Id)).ToArray();
            if (partition.Length != shops.Count || partition.Distinct(StringComparer.Ordinal).Count() != shops.Count || !partition.ToHashSet(StringComparer.Ordinal).SetEquals(shops.Select(x => x.Id!)))
                throw new InvalidOperationException("Roster must partition every shop exactly once.");
            if (row.Dark.Any(x => string.IsNullOrWhiteSpace(x.Reason))) throw new InvalidOperationException("Missing dark-slot explanation.");
            var powered = row.Powered.ToHashSet(StringComparer.Ordinal);
            var load = 0;
            foreach (var shop in shops)
            {
                var id = shop.Id!;
                if (powered.Contains(id)) { load += shop.Kw!.Value; value += shop.ValuePerSlot![slot]; runs[id] = 0; }
                else
                {
                    if (shop.Type == "mandatory") throw new InvalidOperationException("Mandatory load was cut.");
                    darkHours[id]++; runs[id]++;
                    if (runs[id] > input.MaxConsecutiveDark) throw new InvalidOperationException("Dark-run limit exceeded.");
                }
            }
            if (load > input.GeneratorCapacityKw) throw new InvalidOperationException("Capacity exceeded.");
            var used = FuelCents(input, load); fuel += used;
            if (row.LoadKw != load || row.FuelLitres * 100 != used || row.CumulativeFuelLitres * 100 != fuel)
                throw new InvalidOperationException("Incorrect hourly load or fuel totals.");
        }
        if (fuel > Cents(input.FuelBudgetLitres!.Value) || result.FuelTotalLitres * 100 != fuel || result.ValueTotal != value ||
            result.DarkHours.Count != darkHours.Count || darkHours.Any(x => !result.DarkHours.TryGetValue(x.Key, out var count) || count != x.Value))
            throw new InvalidOperationException("Incorrect total fuel, value or dark-hour counts.");
    }
}
