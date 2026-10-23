using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController, Authorize(Roles = "Resident"), Route("api/resident/loadshedding")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResidentScheduleApiController(AppDbContext db, ResidentScheduleQueryService query) : ControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Get([FromQuery, Required, Range(1, int.MaxValue)] int? zoneId, CancellationToken ct)
    {
        var schedule = await query.GetAsync(zoneId!.Value, ct);
        return schedule == null ? NotFound(new { message = "Zone not found. Choose an available zone." }) : Ok(schedule);
    }

    [HttpGet("zones")]
    public async Task<IActionResult> Zones(CancellationToken ct) => Ok(await db.Zones.AsNoTracking().OrderBy(z => z.Code)
        .Select(z => new { z.Id, z.Code, z.Name }).ToListAsync(ct));
}
