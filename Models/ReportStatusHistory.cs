namespace Nagorik.Api.Models;

public class ReportStatusHistory
{
    public int Id { get; set; }

    public int ReportId { get; set; }
    public Report? Report { get; set; }

    public ReportStatus Status { get; set; }

    public ReportStatus? PreviousStatus { get; set; }

    // Nullable for history recorded before actor tracking was introduced.
    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
