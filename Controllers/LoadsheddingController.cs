using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;

namespace Nagorik.Api.Controllers;

[Authorize(Roles = UtilityScheduleAccess.Roles), Route("official/loadshedding")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LoadsheddingController(AppDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await db.Zones.AsNoTracking().OrderBy(z => z.Code).ToListAsync(ct));

}
