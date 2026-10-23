using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Services;
using Nagorik.Api.Authorization;

namespace Nagorik.Api.Controllers;

[ApiController]
[Authorize(Policy = DispatcherMapAccess.Policy)]
[Route("api/map")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MapApiController(RiskZoneQueryService zones) : ControllerBase
{
    [HttpGet("risk-zones")]
    public async Task<IActionResult> RiskZones(CancellationToken ct) =>
        new JsonResult(await zones.ListAsync(ct)) { ContentType = "application/geo+json" };
}
