using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Authorize(Policy = DispatcherMapAccess.Policy)]
[Route("api/map/weather")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MapWeatherController(WeatherReadingQueryService readings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await readings.ListAsync(ct));
}
