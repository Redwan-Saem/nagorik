using Microsoft.AspNetCore.Mvc;

namespace Nagorik.Api.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => Redirect(User.IsInRole("Admin") ? "/admin/risk-thresholds" :
        User.IsInRole("Dispatcher") ? "/dispatcher/map" : User.IsInRole("Crew") ? "/crew/tasks" :
        Nagorik.Api.Authorization.OfficialAccess.IsOfficial(User) ? "/official/reports" : "/map");
}
