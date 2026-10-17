using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

// Shared by the resident API and server-rendered My Reports page.
public static class ResidentReportQueries
{
    public static async Task<ResidentReportPage> ListAsync(AppDbContext db, int userId, int page, CancellationToken ct)
    {
        var query = db.Reports.AsNoTracking().Where(r => r.UserId == userId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * ResidentReportListQuery.PageSize).Take(ResidentReportListQuery.PageSize)
            .Select(r => new ResidentReportListItem(r.Id, r.ReferenceNo ?? $"R-{r.Id:D6}", r.ReferenceNo,
                r.Title, r.Category.ToString(), r.AnomalyType.HasValue ? r.AnomalyType.Value.ToString() : null,
                r.Utility.HasValue ? r.Utility.Value.ToString() : null, ResidentReportStatus.GetLabel(r.Status),
                r.Urgency.ToString(), r.AreaId, r.Area == null ? null : r.Area.Name, r.Location, r.CreatedAt, r.ImageUrl))
            .ToListAsync(ct);
        return new(items, page, ResidentReportListQuery.PageSize, count, (int)Math.Ceiling(count / (double)ResidentReportListQuery.PageSize));
    }
}
