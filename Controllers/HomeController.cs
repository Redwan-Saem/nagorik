using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Nagorik.Api.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/"),HttpGet("/dashboard"),HttpGet("/Home"),HttpGet("/Home/Index")]
    [ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
    public IActionResult Index()=>View(Services.DashboardCatalog.For(User));
    [HttpPost("/session/logout"),Authorize,ValidateAntiForgeryToken]
    public IActionResult Logout(){Response.Cookies.Delete("auth_token",new CookieOptions{Path="/"});return Redirect("/");}
}
