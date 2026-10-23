using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;
[ApiController, Authorize(Policy = DispatcherMapAccess.Policy)]
[Route("api/map/flagged-drainage"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FlaggedDrainageApiController(FlaggedDrainageQueryService query) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(new { points = await query.ListAsync(ct),
        definition = "Drainage points in High/Severe zones evaluated within 15 minutes; maximum 5000 points. Clear points remain listed because their zone is elevated." });
}
