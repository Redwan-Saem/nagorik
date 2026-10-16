using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers;

[Authorize]
[Route("official/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class OfficialReportsController(AppDbContext db) : Controller
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
}
