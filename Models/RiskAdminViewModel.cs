using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Nagorik.Api.Models;

public sealed class RiskAdminViewModel
{
    [Required] public RiskScoringOptions Thresholds { get; set; } = new();
    [Range(0, long.MaxValue)] public long Revision { get; set; }
    [BindNever] public IReadOnlyList<Zone> Zones { get; set; } = [];
}

public sealed class ZoneVulnerabilityInput
{
    [StringLength(300)] public string? Source { get; set; }
    public bool? IsSample { get; set; }
    [Range(0,int.MaxValue)] public int? ExpectedRevision { get; set; }
    [Required, Range(-500, 9000)] public double? ElevationMeters { get; set; }
    [Required, Range(0, 100)] public double? DrainageDensityKmPerSqKm { get; set; }
    [Required, Range(0, 1)] public double? HistoryScore { get; set; }
}
