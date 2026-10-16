namespace Nagorik.Api.Models;

public sealed record ReportDetailViewModel(
    Report Report,
    IReadOnlyList<ReportStatusHistory> StatusHistory);
