using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api")]
    public class NotificationApiController : ControllerBase
    {
        private readonly NotificationSettingsService _service;
        private readonly IConfiguration _config;


        public NotificationApiController(
            NotificationSettingsService service,
            IConfiguration config)
        {
            _service = service;
            _config = config;
        }



        private string UserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)!;




        [HttpGet("notification-settings")]
        public async Task<IActionResult> GetSettings()
        {
            var result = await _service.GetAsync(UserId);

            return Ok(result);
        }




        [HttpPut("notification-settings")]
        public async Task<IActionResult> SaveSettings(
            [FromBody] NotificationSettingsRequest request)
        {
            var result = await _service.SaveAsync(
                UserId,
                request.Enabled,
                request.ZoneIds ?? new List<int>()
            );


            if (!result.ok)
            {
                return BadRequest(new
                {
                    error = result.error
                });
            }


            return Ok(new
            {
                message = "Notification settings saved."
            });
        }




        [HttpPost("push/subscribe")]
        public async Task<IActionResult> Subscribe(
            [FromBody] PushSubscriptionRequest request)
        {
            if (string.IsNullOrEmpty(request.Endpoint) ||
                string.IsNullOrEmpty(request.P256dh) ||
                string.IsNullOrEmpty(request.Auth))
            {
                return BadRequest(new
                {
                    error = "All fields are required."
                });
            }


            await _service.SavePushSubscriptionAsync(
                UserId,
                request.Endpoint,
                request.P256dh,
                request.Auth
            );


            return Ok(new
            {
                message = "Push subscription saved."
            });
        }




        [HttpPost("push/unsubscribe")]
        public async Task<IActionResult> Unsubscribe(
            [FromBody] RemovePushSubscriptionRequest request)
        {
            await _service.RemovePushSubscriptionAsync(
                UserId,
                request.Endpoint
            );


            return Ok(new
            {
                message = "Push subscription removed."
            });
        }




        [HttpGet("push/public-key")]
        public IActionResult PublicKey()
        {
            return Ok(new
            {
                key = _config["Vapid:PublicKey"]
            });
        }
    }




    public class NotificationSettingsRequest
    {
        public bool Enabled { get; set; }

        public List<int>? ZoneIds { get; set; }
    }



    public class PushSubscriptionRequest
    {
        public string Endpoint { get; set; } = "";

        public string P256dh { get; set; } = "";

        public string Auth { get; set; } = "";
    }



    public class RemovePushSubscriptionRequest
    {
        public string Endpoint { get; set; } = "";
    }
}