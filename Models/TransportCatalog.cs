namespace Nagorik.Api.Models;

public sealed class TransportStop
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Source { get; set; } = "";
    public bool IsSample { get; set; }
    public bool Enabled { get; set; } = true;
}
public sealed class TransportLink
{
    public int Id { get; set; }
    public int FromStopId { get; set; }
    public int ToStopId { get; set; }
    public string Mode { get; set; } = "Bus";
    public string Line { get; set; } = "";
    public double DistanceKm { get; set; }
    public int Minutes { get; set; }
    public bool Enabled { get; set; } = true;
    public string Source { get; set; } = "";
    public bool IsSample { get; set; }
}
public sealed class TransportFareRule
{
    public string Mode { get; set; } = "Bus";
    public double BaseBdt { get; set; }
    public double PerKmBdt { get; set; }
    public double MinimumBdt { get; set; }
    public double UpperMultiplier { get; set; } = 1.25;
    public bool IsExact { get; set; }
    public string Source { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}
