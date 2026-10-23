using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ApiController, Route("api/zones")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ZonesController(AppDbContext db, RiskZoneQueryService query) : ControllerBase
{
    [HttpGet("{zoneId}/risk")]
    public async Task<IActionResult> GetRisk(string zoneId, CancellationToken ct)
    {
        var zone = await db.Zones.AsNoTracking().Where(z => z.Code == zoneId)
            .Select(z => new { zoneId = z.Code, zoneName = z.Name, riskLevel = z.RiskLevel.ToString(),
                lastUpdated = z.LastComputedAtUtc, isEvaluated = z.LastComputedAtUtc != null,
                assessment = "Experimental rule-based assessment" }).SingleOrDefaultAsync(ct);
        return zone == null ? NotFound(new { message = "No canonical data for this zone" }) : Ok(zone);
    }
    [HttpGet("map")]
    public async Task<IActionResult> GetMap(CancellationToken ct)
    {
        var data = await query.ListAsync(ct);
        return Ok(new { data.Type, features = data.Features.Select(f => new {
            f.Type, f.Id, f.Geometry, properties = new { f.Properties.Code, f.Properties.Name,
                f.Properties.RiskLevel, f.Properties.LastComputedAtUtc } }), data.InvalidZoneCount });
    }
}
