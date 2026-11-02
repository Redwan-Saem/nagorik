using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;
[ApiController, AllowAnonymous, Route("api/public/map")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicMapApiController(RiskZoneQueryService zones, PublicMapQueryService points) : ControllerBase
{
    [HttpGet("risk-zones")]
    public async Task<IActionResult> Zones(CancellationToken ct)
    {
        var data = await zones.ListAsync(ct);
        return Geo(new { data.Type, Features = data.Features.Select(f => new { f.Type, f.Id, f.Geometry,
            Properties = new { f.Properties.Code, f.Properties.Name, f.Properties.RiskLevel, f.Properties.LastComputedAtUtc,f.Properties.BoundarySource,f.Properties.BoundaryIsSample,f.Properties.InputSource,f.Properties.InputIsSample,f.Properties.GeoRevision } }), data.InvalidZoneCount });
    }
    [HttpGet("reports")] public async Task<IActionResult> Reports(CancellationToken ct) => Geo(await points.ReportsAsync(ct));
    [HttpGet("blockages")] public async Task<IActionResult> Blockages(CancellationToken ct) => Geo(await points.BlockagesAsync(ct));
    private static JsonResult Geo(object data) => new(data) { ContentType = "application/geo+json" };
}
