using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Resident")]
public class NotificationsController : Controller
{
    public IActionResult Settings() => View();
}