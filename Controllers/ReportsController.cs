using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers
{
    [Authorize(Roles = "Resident")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _db;

        public ReportsController(AppDbContext db)
        {
            _db = db;
        }

        public IActionResult Create()
        {
            var vm = new ReportFormViewModel
            {
                Categories = Enum.GetValues<ReportCategory>()
                    .Where(c => c != ReportCategory.UtilityAnomaly)
                    .Select(c => new SelectListItem(
                        c.ToString(),
                        c.ToString()
                    ))
                    .ToList()
            };

            return View(vm);
        }

        [HttpGet("/reports/utility-anomalies/create")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> UtilityCreate(CancellationToken ct)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0 ||
                !await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == "Resident", ct))
                return Unauthorized();
            return View(new UtilityAnomalyFormViewModel(await _db.ReportAreas.AsNoTracking()
                .OrderBy(a => a.Name).ThenBy(a => a.Id).ToListAsync(ct)));
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
                return NotFound();

            var report = await _db.Reports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound();

            return View(report);
        }

        public async Task<IActionResult> Detail(int id, CancellationToken ct)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return NotFound();

            var report = await _db.Reports.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
            if (report == null)
                return NotFound();

            var history = await _db.ReportStatusHistories.AsNoTracking()
                .Where(h => h.ReportId == id)
                .OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id)
                .ToListAsync(ct);

            return View(new ReportDetailViewModel(report, history));
        }

        [HttpGet("/reports/reference/{reference}")]
        public async Task<IActionResult> Reference(string reference, CancellationToken ct)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
                return NotFound();
            if (reference.Length > UtilityAnomalyRules.MaxReferenceLength ||
                !System.Text.RegularExpressions.Regex.IsMatch(reference, "^UA-[0-9]{6,}$")) return NotFound();
            var id = await _db.Reports.AsNoTracking().Where(r => r.UserId == userId && r.ReferenceNo == reference)
                .Select(r => (int?)r.Id).SingleOrDefaultAsync(ct);
            return id == null ? NotFound() : RedirectToAction(nameof(Detail), new { id });
        }

        public async Task<IActionResult> Mine([FromQuery] ResidentReportListQuery request, CancellationToken ct)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0 ||
                !await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == "Resident", ct))
                return Unauthorized();
            if (!ModelState.IsValid) return BadRequest("Provide a valid page number.");
            return View(await ResidentReportQueries.ListAsync(_db, userId, request.Page, ct));
        }
    }
}
