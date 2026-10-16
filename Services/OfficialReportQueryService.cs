using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record OfficialReportListItem(int Id, string Code, string Title, string Category,
    int? AreaId, string? AreaName, string Status, string Urgency, string Location, DateTime CreatedAt);

public sealed record OfficialReportPage(IReadOnlyList<OfficialReportListItem> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);

public class OfficialReportQueryService(AppDbContext db)
{

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
