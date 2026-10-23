using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers;
public sealed class ReportVerificationInput
{
    [Required] public bool? Publish { get; set; }
    [Required, Range(0, long.MaxValue - 1)] public long? ExpectedRevision { get; set; }
}
[Authorize(Policy = DispatcherMapAccess.Policy), Route("dispatcher/report-verification")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportVerificationController(AppDbContext db, TimeProvider clock) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await db.Reports.AsNoTracking()
        .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(100).ToListAsync(ct));
    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, ReportVerificationInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0) return Unauthorized();
        var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (report == null) return NotFound();
        if (input.Publish == true && (!double.IsFinite(report.Latitude) || !double.IsFinite(report.Longitude) ||
            report.Latitude is < -90 or > 90 || report.Longitude is < -180 or > 180 || (report.Latitude == 0 && report.Longitude == 0)))
            return BadRequest("This report has no usable map coordinates and cannot be published.");
        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await db.Reports.Where(r => r.Id == id && r.PublicVerificationRevision == input.ExpectedRevision)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsPubliclyVerified, input.Publish!.Value)
                .SetProperty(r => r.PublicVerifiedAtUtc, now).SetProperty(r => r.PublicVerifiedByUserId, userId)
                .SetProperty(r => r.PublicVerificationRevision, input.ExpectedRevision!.Value + 1), ct);
        if (changed == 0) return Conflict("The verification decision changed. Reload before reviewing again.");
        TempData["VerificationMessage"] = input.Publish == true ? "Marker verified and published. Refresh the public map to see it." : "Marker removed from the public map.";
        return RedirectToAction(nameof(Index));
    }
}
