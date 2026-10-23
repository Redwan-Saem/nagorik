using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Nagorik.Api.Controllers;
[Authorize]
public sealed class PredictionsController : Controller
{
    // Old notification IDs identify legacy risks, not canonical Zone IDs.
    public IActionResult Details(int id) => Redirect("/map");
}
