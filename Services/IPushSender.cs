using WebPush;

namespace Nagorik.Api.Services
{
    public record PushResult(bool Success, int StatusCode);

    public interface IPushSender
    {
        Task<PushResult> SendAsync(
            string endpoint,
            string p256dh,
            string auth,
            string payloadJson);
    }

    public class WebPushSender : IPushSender
    {
        private readonly VapidDetails _vapid;
        private readonly WebPushClient _client = new();

        public WebPushSender(IConfiguration configuration)
        {
            _vapid = new VapidDetails(
                configuration["Vapid:Subject"],
                configuration["Vapid:PublicKey"],
                configuration["Vapid:PrivateKey"]);
        }

        public async Task<PushResult> SendAsync(
            string endpoint,
            string p256dh,
            string auth,
            string payload)
        {
            try
            {
                await _client.SendNotificationAsync(
                    new PushSubscription(endpoint, p256dh, auth),
                    payload,
                    _vapid);

                return new PushResult(true, 201);
            }
            catch (WebPushException ex)
            {
                return new PushResult(false, (int)ex.StatusCode);
            }
        }
    }
}