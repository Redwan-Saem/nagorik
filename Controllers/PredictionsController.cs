using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[Authorize]
public class PredictionsController : Controller
{
    private readonly AppDbContext _db;

    public PredictionsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Details(int id)
    {
        var r = await _db.WaterloggingRisks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r == null)
            return NotFound();

        string window =
            r.ExpectedStartUtc.HasValue && r.ExpectedEndUtc.HasValue
                ? $"{DhakaTime.ToLocal(r.ExpectedStartUtc.Value):h:mm tt} to " +
                  $"{DhakaTime.ToLocal(r.ExpectedEndUtc.Value):h:mm tt}, " +
                  $"{DhakaTime.ToLocal(r.ExpectedStartUtc.Value):d MMM yyyy}"
                : "To be confirmed";

        var vm = new PredictionDetailsViewModel
        {
            Id = r.Id,
            ZoneName = r.ZoneName,
            RiskLevel = r.RiskLevel,
            WindowText = window,
            IssuedAtText = DhakaTime
                .ToLocal(r.LastUpdated)
                .ToString("d MMM yyyy h:mm tt")
        };

        return View(vm);
    }
}