using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public sealed class RiskScoringOptions : IValidatableObject
{
    [Range(.01, 10000)] public double MediumThreshold { get; set; } = 5;
    [Range(.01, 10000)] public double HighThreshold { get; set; } = 15;
    [Range(.01, 10000)] public double SevereThreshold { get; set; } = 30;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!double.IsFinite(MediumThreshold) || !double.IsFinite(HighThreshold) || !double.IsFinite(SevereThreshold) ||
            MediumThreshold <= 0 || MediumThreshold >= HighThreshold || HighThreshold >= SevereThreshold || SevereThreshold > 10000)
            yield return new("Thresholds must be finite, positive and strictly increasing (medium < high < severe <= 10000).");
    }
}
