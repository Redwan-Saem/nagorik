namespace Nagorik.Api.Services;

public class AlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AlertBackgroundService> _log;
    private readonly IConfiguration _cfg;

    public AlertBackgroundService(
        IServiceScopeFactory scopes,
        ILogger<AlertBackgroundService> log,
        IConfiguration cfg)
    {
        _scopes = scopes;
        _log = log;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var secs = _cfg.GetValue(
            "Alerts:PollSeconds",
            15);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();

                var service = scope.ServiceProvider
                    .GetRequiredService<AlertService>();

                await service.ProcessPendingAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Alert processing failed");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(secs),
                ct);
        }
    }
}