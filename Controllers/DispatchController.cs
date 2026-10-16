using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Nagorik.Api.Controllers;

[Authorize(Roles = "Dispatcher")]
public class DispatchController : Controller
{
    private readonly AppDbContext _db;

    public DispatchController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Report(int id)
    {
        var report = await _db.Reports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
            return NotFound();

        return View(report);
    }
}