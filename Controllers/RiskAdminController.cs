using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/risk-thresholds")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RiskAdminController(AppDbContext db, RiskThresholdProvider thresholds,
    RiskComputationService computation, TimeProvider clock, GeographicDataService geography) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var settings = await db.RiskThresholdSettings.AsNoTracking().SingleOrDefaultAsync(ct);
        return View(new RiskAdminViewModel { Thresholds = settings?.ToOptions() ?? await thresholds.GetAsync(ct),
            Revision = settings?.Revision ?? 0, Zones = await db.Zones.AsNoTracking().OrderBy(z => z.Code).ToListAsync(ct) });
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(RiskAdminViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Zones = await db.Zones.AsNoTracking().OrderBy(z => z.Code).ToListAsync(ct);
            Response.StatusCode = 400; return View("Index", model);
        }
        var row = await db.RiskThresholdSettings.SingleOrDefaultAsync(ct);
        if ((row?.Revision ?? 0) != model.Revision) return Conflict("Thresholds changed. Reload before saving.");
        if (row == null) { row = new(); db.RiskThresholdSettings.Add(row); }
        row.MediumThreshold = model.Thresholds.MediumThreshold; row.HighThreshold = model.Thresholds.HighThreshold;
        row.SevereThreshold = model.Thresholds.SevereThreshold; row.Revision++;
        row.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        row.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict("Thresholds changed. Reload before saving."); }
        catch (DbUpdateException) when (model.Revision == 0) { return Conflict("Settings could not be created. Reload before retrying."); }
        TempData["RiskMessage"] = "Thresholds saved. Run evaluation now or wait for the next scheduled cycle.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("zones/{id:int}/vulnerability"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Vulnerability(int id, ZoneVulnerabilityInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { await geography.UpdateVulnerabilityAsync(id,input,User.FindFirstValue(ClaimTypes.NameIdentifier)??"unknown",ct); }
        catch(TransportProblem e){return StatusCode(e.Status,e.Message);}
        TempData["RiskMessage"] = "Vulnerability saved. Run evaluation to calculate a new level.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("recompute"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Recompute(CancellationToken ct)
    {
        var count = await computation.ComputeAsync(ct);
        TempData["RiskMessage"] = $"Evaluated {count} zones. Missing or stale data produces Unknown.";
        return RedirectToAction(nameof(Index));
    }
}
