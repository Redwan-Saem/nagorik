using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;

namespace Nagorik.Api.Controllers;

[ApiController, Authorize(Roles = UtilityScheduleAccess.Roles)]
[Route("api/official/loadshedding")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed partial class LoadsheddingApiController(AppDbContext db, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("zones")]
    public async Task<IActionResult> Zones(CancellationToken ct) => Ok(await db.Zones.AsNoTracking()
        .OrderBy(z => z.Code).Select(z => new { z.Id, z.Code, z.Name }).ToListAsync(ct));

    [HttpGet("security-token")]
    public IActionResult SecurityToken() => Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
}
