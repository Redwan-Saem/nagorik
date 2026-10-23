using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed class WeatherBackgroundService(IServiceScopeFactory scopes, IOptions<WeatherOptions> options,
    TimeProvider clock, ILogger<WeatherBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) { logger.LogInformation("Weather collection is disabled"); return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IWeatherCollector>().CollectAsync(stoppingToken);
                logger.LogInformation("Weather cycle: {Succeeded} succeeded, {Failed} failed, {Skipped} invalid zones", result.SucceededZones, result.FailedZones, result.SkippedZones);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Weather cycle failed; retrying on the next scheduled cycle"); }
            try { await Task.Delay(TimeSpan.FromMinutes(options.Value.PollMinutes), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
