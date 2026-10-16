namespace Nagorik.Api.Models;

public class ReportStatusHistory
{
    public int Id { get; set; }

    public int ReportId { get; set; }
    public Report? Report { get; set; }

    public ReportStatus Status { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}