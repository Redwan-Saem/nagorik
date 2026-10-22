using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers;
public sealed class ResidentAreaInput
{
    [Required] public bool? AlertsEnabled { get; set; }
}
[ApiController, Authorize(Roles = "Resident"), Route("api/resident/areas")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResidentAreasApiController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet("security-token")]
    public IActionResult SecurityToken([FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
        Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    private int? ResidentId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : null;
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (ResidentId is not { } id) return Unauthorized();
        var zones = await db.Zones.AsNoTracking().OrderBy(z => z.Code).Select(z => new { z.Id, z.Code, z.Name }).ToListAsync(ct);
        var subscriptions = await db.ResidentZoneSubscriptions.AsNoTracking().Where(s => s.ResidentId == id)
            .Select(s => new { s.ZoneId, s.AlertsEnabled }).ToListAsync(ct);
        var enabled = await db.NotificationPreferences.AsNoTracking().AnyAsync(p => p.UserId == id.ToString() && p.NotificationsEnabled, ct);
        return Ok(new { zones, subscriptions, globalNotificationsEnabled = enabled });
    }
    [HttpPut("{zoneId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe([Range(1,int.MaxValue)] int zoneId, [FromBody] ResidentAreaInput input, CancellationToken ct)
    {
        if (ResidentId is not { } id) return Unauthorized();
        if (!await db.Users.AnyAsync(u => u.Id == id && u.Role == "Resident",ct)) return Forbid();
        if (!await db.Zones.AnyAsync(z => z.Id == zoneId, ct)) return NotFound(new { message = "Zone not found." });
        var now = clock.GetUtcNow().UtcDateTime;
        // Unique resident/zone identity makes repeated or simultaneous subscribe idempotent.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ResidentZoneSubscriptions (ResidentId, ZoneId, AlertsEnabled, CreatedAtUtc, UpdatedAtUtc) VALUES ({id}, {zoneId}, {input.AlertsEnabled!.Value}, {now}, {now}) ON CONFLICT(ResidentId, ZoneId) DO UPDATE SET AlertsEnabled = excluded.AlertsEnabled, UpdatedAtUtc = excluded.UpdatedAtUtc", ct);
        return Ok(new { zoneId, alertsEnabled = input.AlertsEnabled.Value });
    }
    [HttpDelete("{zoneId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsubscribe([Range(1,int.MaxValue)] int zoneId, CancellationToken ct)
    {
        if (ResidentId is not { } id) return Unauthorized();
        var code = await db.Zones.Where(z => z.Id == zoneId).Select(z => z.Code).SingleOrDefaultAsync(ct);
        if (code == null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.ResidentZoneSubscriptions.Where(s => s.ResidentId == id && s.ZoneId == zoneId).ExecuteDeleteAsync(ct);
        // A legacy opt-in must not silently re-enable alerts after an explicit unsubscribe.
        await db.AreaSubscriptions.Where(s => s.UserId == id.ToString() && s.ZoneId == code).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct); return NoContent();
    }
}
