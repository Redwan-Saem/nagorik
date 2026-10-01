using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Models;
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

        // T-002.5: Development-only risk creation endpoint
        [HttpPost("create-risk")]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateRisk(
            string zoneId,
            string level,
            [FromServices] AppDbContext db)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var risk = new WaterloggingRisk
            {
                ZoneId = zoneId,
                ZoneName = $"Zone {zoneId}",
                RiskLevel = level,
                Latitude = 23.8103,
                Longitude = 90.4125,
                LastUpdated = DateTime.UtcNow,
                ExpectedStartUtc = DateTime.UtcNow.AddHours(1),
                ExpectedEndUtc = DateTime.UtcNow.AddHours(4)
            };

            db.WaterloggingRisks.Add(risk);
            await db.SaveChangesAsync();

            return Ok(new { id = risk.Id });
        }
    }
}