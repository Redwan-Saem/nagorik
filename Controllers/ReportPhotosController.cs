using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Authorize(Roles = "Resident,Crew,Dispatcher,Official,CityOfficial")]
[Route("api/report-photos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportPhotosController(AppDbContext db, IPhotoStorage storage) : ControllerBase
{
    [HttpGet("{fileName}")]
    public async Task<IActionResult> Get(string fileName, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Unauthorized();
        var url = LocalPhotoStorage.PrivateUrlPrefix + fileName;
        if (!LocalPhotoStorage.IsPrivateUrl(url)) return NotFound();
        var reports = db.Reports.AsNoTracking().Where(r => r.PhotoPaths.Contains(url) || r.ImageUrl == url);
        if (!OfficialAccess.IsOfficial(User) && !User.IsInRole("Dispatcher"))
            reports = reports.Where(r => (User.IsInRole("Resident") && r.UserId == userId) ||
                (User.IsInRole("Crew") && r.AssignedCrew != null && r.AssignedCrew.UserId == userId));
        if (!await reports.AnyAsync(ct)) return NotFound();
        var photo = await storage.OpenPrivateAsync(url, ct);
        if (photo == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(photo.Content, photo.ContentType);
    }
}
