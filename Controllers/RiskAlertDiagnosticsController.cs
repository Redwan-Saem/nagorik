using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Controllers;
[Authorize(Roles="Admin"),Route("admin/risk-alert-deliveries")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class RiskAlertDiagnosticsController(AppDbContext db):Controller
{
    [HttpGet]public async Task<IActionResult> Index(CancellationToken ct)=>View(await db.RiskAlertDeliveries.AsNoTracking()
        .OrderByDescending(d=>d.Id).Take(100).ToListAsync(ct));
}
