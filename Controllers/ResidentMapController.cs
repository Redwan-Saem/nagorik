using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Nagorik.Api.Controllers;
[AllowAnonymous, Route("map")]
public sealed class ResidentMapController : Controller
{
    [HttpGet("")] public IActionResult Index() => View();
}
