using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public class SosLocationRequest : IValidatableObject
{
    [Range(-90d, 90d)]
    public double? Latitude { get; set; }
    [Range(-180d, 180d)]
    public double? Longitude { get; set; }
    [StringLength(500)]
    public string? AddressText { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Latitude.HasValue != Longitude.HasValue)
            yield return new("Provide both latitude and longitude, or omit both.", new[] { nameof(Latitude), nameof(Longitude) });
        if ((Latitude.HasValue && !double.IsFinite(Latitude.Value)) ||
            (Longitude.HasValue && !double.IsFinite(Longitude.Value)))
            yield return new("Coordinates must be finite numbers.", new[] { nameof(Latitude), nameof(Longitude) });
        if (!Latitude.HasValue && !Longitude.HasValue && string.IsNullOrWhiteSpace(AddressText))
            yield return new("Provide GPS coordinates or a manual address.", new[] { nameof(AddressText) });
    }
}

public sealed class SosTriggerRequest : SosLocationRequest
{
    public Guid? ClientRequestId { get; set; }
}
