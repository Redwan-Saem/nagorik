using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Authorize(Policy = DispatcherMapAccess.Policy)]
[Route("api/map")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MapFeaturesApiController(MapFeatureQueryService features) : ControllerBase
{
    [HttpGet("drainage-points")]
    public async Task<IActionResult> Drainage(CancellationToken ct) =>
        new JsonResult(await features.DrainageAsync(ct)) { ContentType = "application/geo+json" };

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(CancellationToken ct) =>
        new JsonResult(await features.ReportsAsync(ct)) { ContentType = "application/geo+json" };
}
