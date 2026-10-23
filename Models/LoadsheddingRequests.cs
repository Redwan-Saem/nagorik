using System.ComponentModel.DataAnnotations;
using Nagorik.Api.Services;

namespace Nagorik.Api.Models;

public sealed class ScheduleDemandRequest
{
    [Range(1, int.MaxValue)] public int ZoneId { get; set; }
    [Range(0.000001, 1000000)] public double DemandMw { get; set; }
}

public sealed class GenerateScheduleRequest : IValidatableObject
{
    [Required] public DateOnly? StartDate { get; set; }
    [Required] public DateOnly? EndDate { get; set; }
    [Required, Range(0.000001, 1000000)] public double? AvailableCapacityMw { get; set; }
    public int SlotMinutes { get; set; } = 120;
    [Required, MinLength(1), MaxLength(100)] public List<ScheduleDemandRequest> Zones { get; set; } = [];

    public RotationInput ToInput() => new(StartDate!.Value, EndDate!.Value, AvailableCapacityMw!.Value,
        SlotMinutes, Zones.Select(x => new ZoneDemandInput(x.ZoneId, x.DemandMw)).ToArray());

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartDate == null || EndDate == null || AvailableCapacityMw == null || Zones == null) yield break;
        string? error = null;
        try { LoadsheddingRotationEngine.ValidateInput(ToInput()); }
        catch (ArgumentException ex) { error = ex.Message; }
        if (error != null) yield return new ValidationResult(error);
    }
}

public sealed class EditScheduleSlotRequest
{
    [Required, Range(1, int.MaxValue)] public int? ExpectedRevision { get; set; }
    [Range(0, int.MaxValue)] public int SlotId { get; set; }
    [Range(1, int.MaxValue)] public int ZoneId { get; set; }
    [Required] public DateOnly? Date { get; set; }
    [Range(0, 1439)] public int StartMinute { get; set; }
    [Range(1, 1440)] public int EndMinute { get; set; }
    public bool Remove { get; set; }
}

public sealed class PublishScheduleRequest
{
    [Required, Range(1, int.MaxValue)] public int? ExpectedRevision { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm publication explicitly.")] public bool Confirm { get; set; }
    public bool AcknowledgeWarnings { get; set; }
}
