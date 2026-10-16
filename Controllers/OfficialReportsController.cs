using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[Authorize]
[Route("official/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class OfficialReportsController(AppDbContext db, OfficialReportQueryService reports) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (OfficialAccess.IsOfficial(User))
            return View(new OfficialReportFiltersViewModel(
                await db.ReportAreas.AsNoTracking().OrderBy(a => a.Name).ThenBy(a => a.Id).ToListAsync(ct)));
        Response.StatusCode = 403;
        return View("AccessDenied");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, string? returnUrl, CancellationToken ct)
    {
        if (!OfficialAccess.IsOfficial(User))
        {
            Response.StatusCode = 403;
            return View("AccessDenied");
        }

        var listUrl = Url.IsLocalUrl(returnUrl) &&
            (returnUrl == "/official/reports" || returnUrl!.StartsWith("/official/reports?", StringComparison.Ordinal))
            ? returnUrl : "/official/reports";
        var report = id > 0 ? await reports.GetByIdAsync(id, ct) : null;
        if (report == null) Response.StatusCode = 404;
        return View(new OfficialReportDetailViewModel(report, listUrl!));
    }
}
