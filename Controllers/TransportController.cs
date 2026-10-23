using Microsoft.AspNetCore.Mvc;
namespace Nagorik.Api.Controllers;
public sealed class TransportController:Controller
{
    [HttpGet("/transport"),HttpGet("/transport/route")]
    public IActionResult Index()=>View();
    [HttpGet("/transport/occupancy")]
    public IActionResult Occupancy()=>View();
}
