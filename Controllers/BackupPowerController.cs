using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

// Deliberately not ApiController: formatter/model errors use this module's
// invalid_input contract, without changing existing API behavior globally.
public sealed class BackupPowerController(BackupPowerSolver solver, AppDbContext db,
    ResidentScheduleQueryService schedules) : Controller
{
    [HttpGet("/backup-power")]
    public IActionResult Index() => View();

    [HttpPost("/api/roster"), RequestSizeLimit(128 * 1024)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Roster([FromBody] BackupPowerInput? input, CancellationToken ct)
    {
        var errors = BackupPowerRules.Validate(input);
        foreach (var item in ModelState.Where(x => x.Value?.Errors.Count > 0))
            errors[item.Key.Length == 0 ? "body" : item.Key] = "Invalid JSON field type or value. " + item.Value!.Errors.First().ErrorMessage;
        if (errors.Count != 0) return new JsonResult(new { status = "invalid_input", message = "Correct the named fields.", errors }, BackupPowerJson.Options) { StatusCode = 400 };
        var result = solver.Solve(input!, ct);
        return new JsonResult(result, BackupPowerJson.Options)
        {
            StatusCode = result.Status switch { "busy" => 429, "not_solved" => 503, _ => 200 }
        };
    }

    [HttpGet("/api/backup-power/published-grid")]
    [Authorize(Roles = "Resident," + UtilityScheduleAccess.Roles)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PublishedGrid(int? zoneId, string? date, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Unauthorized();
        var role = await db.Users.AsNoTracking().Where(u => u.Id == actor).Select(u => u.Role).SingleOrDefaultAsync(ct);
        if (role is not ("Resident" or "UtilityOfficial" or "DPDCOfficial" or "DESCOOfficial")) return Forbid();
        if (!ModelState.IsValid || zoneId is not > 0 || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return BadRequest(new { message = "Choose zoneId and an ISO date (yyyy-MM-dd)." });
        var week = await schedules.GetAsync(zoneId.Value, ct);
        if (week is null) return NotFound(new { message = "Zone not found." });
        var selected = week.Days.SingleOrDefault(d => d.Date == day);
        if (selected is null) return BadRequest(new { message = "Choose a date within the current published seven-day view." });
        if (!selected.IsPublished) return Conflict(new { message = "This zone/date has no published plan. Unknown cannot be converted to grid-on hours. Enter reviewed outage hours manually." });
        if (selected.Outages.Any(o => o.StartMinute % 60 != 0 || o.EndMinute % 60 != 0))
            return Conflict(new { message = "The published plan contains partial-hour windows. This hourly planner cannot import them without approximation; enter a reviewed hourly scenario manually." });
        var grid = Enumerable.Range(10, 10).Select(h => !selected.Outages.Any(o => o.StartMinute < (h + 1) * 60 && o.EndMinute > h * 60)).ToArray();
        return Ok(new { grid_available = grid, zone_id = zoneId, zone_name = week.ZoneName, date = day,
            source = "Nagorik published loadshedding plan", published_at_utc = selected.PublishedAtUtc,
            message = "Snapshot for 10:00–20:00 Asia/Dhaka. This plan is not a live grid measurement. Import again and regenerate after a schedule change." });
    }
}
