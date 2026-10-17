using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public sealed class CreateUtilityAnomalyRequest : IValidatableObject
{
    [Required, EnumDataType(typeof(AnomalyType))]
    public AnomalyType? AnomalyType { get; set; }
    [Required, StringLength(1000, MinimumLength = 10)]
    public string Description { get; set; } = "";
    [Required, StringLength(300)]
    public string AddressText { get; set; } = "";
    [Required, Range(1, int.MaxValue)]
    public int? AreaId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    [Required, MinLength(1), MaxLength(3)]
    public List<IFormFile> Photos { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Description.Trim().Length is < 10 or > 1000)
            yield return new("Description must contain 10–1000 characters after trimming.", [nameof(Description)]);
        if (Latitude.HasValue != Longitude.HasValue ||
            Latitude.HasValue && (!double.IsFinite(Latitude.Value) || Latitude is < -90 or > 90) ||
            Longitude.HasValue && (!double.IsFinite(Longitude.Value) || Longitude is < -180 or > 180))
            yield return new("Provide both coordinates within valid latitude/longitude ranges, or omit both.", [nameof(Latitude), nameof(Longitude)]);
    }
}
