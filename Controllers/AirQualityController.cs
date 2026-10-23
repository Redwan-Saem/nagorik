using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Controllers;
public sealed class AirQualityController(AppDbContext db):Controller
{
    [HttpGet("/air-quality")]
    public async Task<IActionResult> Index(CancellationToken ct)=>View(await db.Zones.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new ServiceZone(x.Id,x.Name)).ToListAsync(ct));
}
