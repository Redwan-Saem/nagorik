using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public sealed class ResidentReportListQuery
{
    public const int PageSize = 20;
    [Range(1, int.MaxValue / PageSize)]
    public int Page { get; set; } = 1;
}

public sealed record ResidentReportListItem(int Id, string Code, string? ReferenceNo, string Title,
    string Category, string? AnomalyType, string? Utility, string Status, string Urgency,
    int? AreaId, string? AreaName, string Location, DateTime CreatedAt);

public sealed record ResidentReportPage(IReadOnlyList<ResidentReportListItem> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);

public static class ResidentReportStatus
{
    public static string GetLabel(ReportStatus status) => status switch
    {
        ReportStatus.Pending => "Submitted",
        ReportStatus.Approved or ReportStatus.Assigned => "Acknowledged",
        ReportStatus.InProgress => "In Progress",
        ReportStatus.Completed => "Resolved",
        ReportStatus.Rejected => "Rejected",
        _ => "Unknown"
    };
}
