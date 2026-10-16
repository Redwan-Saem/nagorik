using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record OfficialReportListItem(int Id, string Code, string Title, string Category,
    int? AreaId, string? AreaName, string Status, string Urgency, string Location, DateTime CreatedAt);

public sealed record OfficialReportPage(IReadOnlyList<OfficialReportListItem> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record OfficialReportHistoryItem(string Status, string? PreviousStatus,
    int? ChangedByUserId, DateTime ChangedAtUtc);

public sealed record OfficialReportDetailDto(int Id, string Code, string Title, string Description,
    string Category, string Authority, string Status, string Urgency, int? AreaId, string? AreaName,
    string Location, double Latitude, double Longitude, string? ImageUrl, DateTime CreatedAt,
    int? AssignedCrewId, string? AssignedCrewName, IReadOnlyList<OfficialReportHistoryItem> StatusHistory);

public class OfficialReportQueryService(AppDbContext db)
{
    public async Task<OfficialReportDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var report = await db.Reports.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new OfficialReportDetailDto(r.Id, $"R-{r.Id:D6}", r.Title, r.Description,
                r.Category.ToString(), r.Authority.ToString(), r.Status.ToString(), r.Urgency.ToString(),
                r.AreaId, r.Area == null ? null : r.Area.Name, r.Location, r.Latitude, r.Longitude,
                string.IsNullOrWhiteSpace(r.ImageUrl) ? null : r.ImageUrl, r.CreatedAt,
                r.AssignedCrewId, r.AssignedCrew == null ? null : r.AssignedCrew.Name,
                Array.Empty<OfficialReportHistoryItem>()))
            .SingleOrDefaultAsync(ct);
        if (report == null) return null;
        var history = await db.ReportStatusHistories.AsNoTracking().Where(h => h.ReportId == id)
            .OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id)
            .Select(h => new OfficialReportHistoryItem(h.Status.ToString(),
                h.PreviousStatus.HasValue ? h.PreviousStatus.Value.ToString() : null,
                h.ChangedByUserId, h.ChangedAtUtc))
            .ToListAsync(ct);
        return report with { StatusHistory = history };
    }

    public async Task<OfficialReportPage> ListAsync(OfficialReportListQuery request, CancellationToken ct = default)
    {
        var query = db.Reports.AsNoTracking().AsQueryable();
        if (request.Categories.Length > 0)
        {
            var categories = request.Categories.Select(c => Enum.Parse<ReportCategory>(c, true)).Distinct().ToArray();
            query = query.Where(r => categories.Contains(r.Category));
        }
        if (request.AreaId.HasValue) query = query.Where(r => r.AreaId == request.AreaId);
        var total = await query.CountAsync(ct);
        var ascending = request.Direction == "asc";
        IOrderedQueryable<Report> ordered = request.Sort switch
        {
            // Status uses persisted status names alphabetically. Urgency uses its numeric priority.
            "status" => ascending ? query.OrderBy(r => r.Status) : query.OrderByDescending(r => r.Status),
            "urgency" => ascending ? query.OrderBy(r => r.Urgency) : query.OrderByDescending(r => r.Urgency),
            _ => ascending ? query.OrderBy(r => r.CreatedAt) : query.OrderByDescending(r => r.CreatedAt)
        };
        ordered = request.Sort == "date"
            ? ascending ? ordered.ThenBy(r => r.Id) : ordered.ThenByDescending(r => r.Id)
            : ordered.ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        var items = await ordered.Skip((request.Page - 1) * OfficialReportListQuery.PageSize)
            .Take(OfficialReportListQuery.PageSize)
            .Select(r => new OfficialReportListItem(r.Id, $"R-{r.Id:D6}", r.Title, r.Category.ToString(),
                r.AreaId, r.Area == null ? null : r.Area.Name, r.Status.ToString(), r.Urgency.ToString(), r.Location, r.CreatedAt))
            .ToListAsync(ct);
        return new(items, request.Page, OfficialReportListQuery.PageSize, total,
            (int)Math.Ceiling(total / (double)OfficialReportListQuery.PageSize));
    }
}
