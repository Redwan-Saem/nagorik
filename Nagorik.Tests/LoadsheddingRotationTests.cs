using System.Diagnostics;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Tests;

public sealed class LoadsheddingRotationTests
{
    private static RotationInput Input(int count = 4, double capacity = 20) => new(new(2026, 10, 23), new(2026, 10, 29), capacity, 120,
        Enumerable.Range(1, count).Select(id => new ZoneDemandInput(id, 10)).ToArray());

    [Fact]
    public void RotationIsCapacitySafeFairAndDeterministic()
    {
        var engine = new LoadsheddingRotationEngine(); var input = Input();
        var slots = engine.Generate(input); var review = engine.Review(input, slots.ToArray());
        Assert.True(review.IsValid, string.Join(";", review.Errors));
        var counts = slots.GroupBy(s => s.ZoneId).Select(g => g.Count()).ToArray();
        Assert.True(counts.Max() - counts.Min() <= 1);
        Assert.Equal(slots.Select(s => (s.ZoneId, s.Date, s.StartMinute)), engine.Generate(input).Select(s => (s.ZoneId, s.Date, s.StartMinute)));
        Assert.All(slots.GroupBy(s => (s.Date, s.StartMinute)), g => Assert.Equal(2, g.Count()));
    }

    [Fact]
    public void EnoughCapacityProducesExplicitNoOutages()
    {
        var engine = new LoadsheddingRotationEngine();var input = Input(capacity: 40);
        Assert.Empty(engine.Generate(input)); Assert.True(engine.Review(input, []).IsValid);
    }

    [Fact]
    public void EditedMissingCapacityAndInvalidTimeGridAreRejected()
    {
        var engine = new LoadsheddingRotationEngine();var input = Input();var slots = engine.Generate(input).ToList();
        slots.RemoveAt(0);Assert.False(engine.Review(input, slots).IsValid);
        slots.Add(new() { ZoneId = 1, Date = input.StartDate, StartMinute = 5, EndMinute = 125 });
        Assert.Contains("time grid", engine.Review(input, slots).Errors.Single());
    }

    [Fact]
    public void HeterogeneousHundredZoneMonthFitsPerformanceBudget()
    {
        var input = Input(100, 500) with { EndDate = new(2026,11,22), Zones = Enumerable.Range(1,100).Select(id=>new ZoneDemandInput(id, id % 20 + 1)).ToArray() };
        var engine=new LoadsheddingRotationEngine();var watch=Stopwatch.StartNew();var slots=engine.Generate(input);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Generation took {watch.Elapsed}");
        Assert.True(engine.Review(input, slots.ToArray()).IsValid);
        var counts=input.Zones.Select(z=>slots.Count(s=>s.ZoneId==z.ZoneId)).ToArray();Assert.True(counts.Max()-counts.Min()<=1);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidCapacityIsRejected(double capacity) => Assert.Throws<ArgumentException>(() => new LoadsheddingRotationEngine().Generate(Input(capacity:capacity)));
}
