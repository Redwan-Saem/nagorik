using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers
{
    [Route("dev")]
    public class DevController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notificationService;

        public DevController(
            IWebHostEnvironment env,
            INotificationService notificationService)
        {
            _env = env;
            _notificationService = notificationService;
        }

        [Authorize]
        [HttpPost("test-push")]
        public async Task<IActionResult> TestPush()
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var sent = await _notificationService.SendToUserAsync(
                userId,
                "Test",
                "Push works!",
                "/");

            return Ok(new { sent });
        }
    }
}