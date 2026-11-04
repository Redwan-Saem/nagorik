using System.Text.Json.Serialization;

namespace Nagorik.Api.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class BackupPowerInput
{
    [JsonPropertyName("grid_available")] public bool[]? GridAvailable { get; set; }
    [JsonPropertyName("generator_capacity_kw")] public int? GeneratorCapacityKw { get; set; }
    [JsonPropertyName("fuel_budget_litres")] public decimal? FuelBudgetLitres { get; set; }
    [JsonPropertyName("fuel_table")] public List<GeneratorFuelBand>? FuelTable { get; set; }
    [JsonPropertyName("max_consecutive_dark")] public int? MaxConsecutiveDark { get; set; }
    [JsonPropertyName("shops")] public List<BackupPowerShop>? Shops { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class GeneratorFuelBand
{
    [JsonPropertyName("load_kw")] public int? LoadKw { get; set; }
    [JsonPropertyName("litres_per_hour")] public decimal? LitresPerHour { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class BackupPowerShop
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("kw")] public int? Kw { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("value_per_slot")] public int[]? ValuePerSlot { get; set; }
}

public sealed record DarkPowerEntry(string Id, string Reason);
public sealed record BackupPowerHour(int Slot, string Hour, IReadOnlyList<string> Powered,
    IReadOnlyList<DarkPowerEntry> Dark, int LoadKw, decimal FuelLitres, decimal CumulativeFuelLitres);
public sealed record BackupPowerResult(string Status, string Message, IReadOnlyList<BackupPowerHour> Roster,
    long ValueTotal, decimal FuelTotalLitres, IReadOnlyDictionary<string, int> DarkHours,
    bool OptimalityProven, string SolverState, long? ValueUpperBound = null);

// This module deliberately exposes a documented snake_case JSON contract without
// altering the rest of Nagorik's serializer configuration.
public static class BackupPowerJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };
}
