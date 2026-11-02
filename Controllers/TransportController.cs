using Microsoft.AspNetCore.Mvc;
namespace Nagorik.Api.Controllers;
public sealed class TransportController:Controller
{
    [HttpGet("/transport"),HttpGet("/transport/route")]
    public IActionResult Index()=>View();
}
