using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nagorik.Api.Controllers;

[Authorize(Roles = "Dispatcher")]
[Route("dispatcher/map")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DispatcherMapController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
