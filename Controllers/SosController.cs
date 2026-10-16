using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[Authorize]
[Route("sos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SosController(SosAlertService service, SosResponderDirectory responders, AppDbContext db) : Controller
{


    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Unauthorized();
        return View(await service.GetActiveAsync(userId, ct));
    }

    [HttpGet("respond/{id:int}")]
    public async Task<IActionResult> Respond(int id, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Unauthorized();
        if (!await responders.IsResponderAsync(userId, ct)) return Forbid();
        var alert = await db.SosAlerts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        return alert == null ? NotFound() : View(alert);
    }
}
