using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed class RiskBackgroundService(IServiceScopeFactory scopes, IOptions<RiskJobOptions> options,
    TimeProvider clock, ILogger<RiskBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<RiskComputationService>().ComputeAsync(stoppingToken);
                logger.LogInformation("Updated risk for {Count} zones", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Risk computation failed; retrying next cycle"); }
            try { await Task.Delay(TimeSpan.FromMinutes(options.Value.PollMinutes), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
