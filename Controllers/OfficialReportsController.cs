using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;

namespace Nagorik.Api.Controllers;

[Authorize]
[Route("official/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class OfficialReportsController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        if (OfficialAccess.IsOfficial(User)) return View();
        Response.StatusCode = 403;
        return View("AccessDenied");
    }
}
