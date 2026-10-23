using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record ZoneDemandInput(int ZoneId, double DemandMw);
public sealed record RotationInput(DateOnly StartDate, DateOnly EndDate, double AvailableCapacityMw,
    int SlotMinutes, IReadOnlyList<ZoneDemandInput> Zones);
public sealed record RotationReview(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

// Capacity is the maximum supplied MW. Whole-zone switching can exceed the
// required shed MW, but supplied demand must never exceed available capacity.
public sealed class LoadsheddingRotationEngine
{
    public static void ValidateInput(RotationInput input)
    {
        if (input.StartDate.Year < 2000 || input.EndDate.Year > 2100 || input.EndDate < input.StartDate || input.EndDate.DayNumber - input.StartDate.DayNumber >= 31)
            throw new ArgumentException("Choose a period of 1 to 31 days.");
        if (!double.IsFinite(input.AvailableCapacityMw) || input.AvailableCapacityMw <= 0 || input.AvailableCapacityMw > 1000000)
            throw new ArgumentException("Available capacity must be a positive finite MW value, at most 1,000,000.");
        if (input.SlotMinutes is not (60 or 120 or 180 or 240 or 360))
            throw new ArgumentException("Choose a 60, 120, 180, 240 or 360 minute slot.");
        if (input.Zones.Count is < 1 or > 100 || input.Zones.Select(x => x.ZoneId).Distinct().Count() != input.Zones.Count ||
            input.Zones.Any(x => x.ZoneId <= 0 || !double.IsFinite(x.DemandMw) || x.DemandMw <= 0 || x.DemandMw > 1000000))
            throw new ArgumentException("Choose 1 to 100 distinct zones with positive finite demand in MW, at most 1,000,000 each.");
    }

    public IReadOnlyList<LoadsheddingSlot> Generate(RotationInput input)
    {
        ValidateInput(input);
        var zones = input.Zones.OrderBy(x => x.ZoneId).ToArray();
        var counts = zones.ToDictionary(x => x.ZoneId, _ => 0);
        var prior = new HashSet<(int Zone, int Minute)>();
        var result = new List<LoadsheddingSlot>();
        var shortage = Math.Max(0, zones.Sum(x => x.DemandMw) - input.AvailableCapacityMw);
        for (var day = input.StartDate; day <= input.EndDate; day = day.AddDays(1))
        {
            var current = new HashSet<(int, int)>();
            for (var minute = 0; minute < 1440; minute += input.SlotMinutes)
            {
                var offset = ((day.DayNumber - input.StartDate.DayNumber) + minute / input.SlotMinutes) % zones.Length;
                var chosen = zones.Select((z, i) => new { z, i }).OrderBy(x => counts[x.z.ZoneId])
                    .ThenBy(x => prior.Contains((x.z.ZoneId, minute)))
                    .ThenBy(x => (x.i - offset + zones.Length) % zones.Length);
                double shed = 0;
                foreach (var item in chosen)
                {
                    if (shed + 0.000001 >= shortage) break;
                    var id = item.z.ZoneId;
                    result.Add(new() { ZoneId = id, Date = day, StartMinute = minute, EndMinute = minute + input.SlotMinutes });
                    shed += item.z.DemandMw; counts[id]++; current.Add((id, minute));
                }
            }
            prior = current;
        }
        return result;
    }

    public RotationReview Review(RotationInput input, IReadOnlyCollection<LoadsheddingSlot> slots)
    {
        ValidateInput(input);
        var errors = new List<string>(); var warnings = new List<string>();
        var demands = input.Zones.ToDictionary(x => x.ZoneId, x => x.DemandMw);
        if (slots.Any(x => !demands.ContainsKey(x.ZoneId) || x.Date < input.StartDate || x.Date > input.EndDate ||
            x.StartMinute < 0 || x.StartMinute >= 1440 || x.StartMinute % input.SlotMinutes != 0 ||
            x.EndMinute != x.StartMinute + input.SlotMinutes))
            return new(["Every slot must use a selected zone, an included date and the configured time grid."], []);
        if (slots.GroupBy(x => (x.ZoneId, x.Date, x.StartMinute)).Any(g => g.Count() > 1))
            errors.Add("A zone has duplicate outage slots.");
        var byTime = slots.GroupBy(x => (x.Date, x.StartMinute)).ToDictionary(g => g.Key, g => g.Select(x => x.ZoneId).Distinct().ToArray());
        var total = demands.Values.Sum(); var uncovered = 0;
        for (var day = input.StartDate; day <= input.EndDate; day = day.AddDays(1))
            for (var minute = 0; minute < 1440; minute += input.SlotMinutes)
            {
                var shed = byTime.TryGetValue((day, minute), out var off) ? off.Sum(id => demands[id]) : 0;
                if (total - shed > input.AvailableCapacityMw + 0.000001) uncovered++;
            }
        if (uncovered > 0) errors.Add($"Supplied demand exceeds capacity in {uncovered} time slots. Restore enough outage capacity before publishing.");
        var countByZone = slots.GroupBy(x => x.ZoneId).ToDictionary(g => g.Key, g => g.Count());
        var counts = demands.Keys.Select(id => countByZone.GetValueOrDefault(id)).ToArray();
        if (counts.Max() - counts.Min() > 1) warnings.Add("Outage time differs between zones by more than one slot. Review fairness before publishing.");
        var occupied = slots.Select(s => (s.ZoneId, s.Date, s.StartMinute)).ToHashSet();
        var repeated = slots.Count(s => occupied.Contains((s.ZoneId, s.Date.AddDays(-1), s.StartMinute)));
        if (repeated > 0) warnings.Add($"{repeated} zone/time slots repeat on consecutive days. Whole-zone demand and fairness constraints may require repeats; review them explicitly.");
        if (input.Zones.Sum(x => x.DemandMw) > input.AvailableCapacityMw)
            warnings.Add("Whole-zone switching can shed more MW than the exact shortfall. This planning model uses constant user-entered zone demand; it is not a live grid-control system.");
        return new(errors, warnings);
    }
}
