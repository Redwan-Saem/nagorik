using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;
[ApiController, Authorize(Policy = DispatcherMapAccess.Policy)]
[Route("api/map/crews"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MapCrewsController(CrewMapQueryService crews) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) =>
        new JsonResult(await crews.ListAsync(ct)) { ContentType = "application/geo+json" };
}
