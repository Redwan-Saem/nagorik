using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers;

[Authorize(Roles = "Crew")]
[Route("crew/tasks")]
public class CrewTasksController(AppDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Unauthorized();

        var reports = await db.Reports.AsNoTracking()
            .Where(r => r.AssignedCrew != null && r.AssignedCrew.UserId == userId)
            .OrderBy(r => r.Status == ReportStatus.Completed)
            .ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .ToListAsync(ct);
        return View(reports);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Unauthorized();

        var report = await db.Reports.AsNoTracking().FirstOrDefaultAsync(r =>
            r.Id == id && r.AssignedCrew != null && r.AssignedCrew.UserId == userId, ct);
        if (report == null) return NotFound();

        var history = await db.ReportStatusHistories.AsNoTracking()
            .Where(h => h.ReportId == id)
            .OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id).ToListAsync(ct);
        return View(new ReportDetailViewModel(report, history));
    }
}
