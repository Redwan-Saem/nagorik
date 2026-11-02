using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nagorik.Api.Controllers;

[Authorize(Roles = "Resident"), Route("resident/loadshedding")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResidentScheduleController : Controller
{
    [HttpGet("")] public IActionResult Index() => View();
}
