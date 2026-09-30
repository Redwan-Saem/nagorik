using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers
{
    [Authorize(Roles = "Resident")]
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
                    .Select(c => new SelectListItem(
                        c.ToString(),
                        c.ToString()
                    ))
                    .ToList()
            };

            return View(vm);
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

        public async Task<IActionResult> Mine()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
                return View(new List<Report>());

            var list = await _db.Reports
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(list);
        }
    }
}