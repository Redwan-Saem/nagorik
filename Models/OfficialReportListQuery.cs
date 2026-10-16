using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public sealed class OfficialReportListQuery : IValidatableObject
{
    public const int PageSize = 20;
    [Range(1, int.MaxValue / PageSize)]
    public int Page { get; set; } = 1;
    [MaxLength(7)]
    public string[] Categories { get; set; } = [];
    [Range(1, int.MaxValue)]
    public int? AreaId { get; set; }
    [Required, RegularExpression("^(date|status|urgency)$")]
    public string Sort { get; set; } = "date";
    [Required, RegularExpression("^(asc|desc)$")]
    public string Direction { get; set; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var valid = Enum.GetNames<ReportCategory>();
        if (Categories.Any(c => !valid.Contains(c, StringComparer.OrdinalIgnoreCase)))
            yield return new("Categories must use valid report category names.", new[] { nameof(Categories) });
    }
}
