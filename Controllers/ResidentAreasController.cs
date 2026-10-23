using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Nagorik.Api.Controllers;
[Authorize(Roles = "Resident"), Route("resident/areas")]
public sealed class ResidentAreasController : Controller
{
    [HttpGet("")] public IActionResult Index() => View();
}
