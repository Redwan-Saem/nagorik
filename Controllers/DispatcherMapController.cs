using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;

namespace Nagorik.Api.Controllers;

[Authorize(Policy = DispatcherMapAccess.Policy)]
[Route("dispatcher/map")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DispatcherMapController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
