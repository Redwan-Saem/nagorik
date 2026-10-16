using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nagorik.Api.Models;

// Pauses one request after it has read/validated the report but before its transaction starts.
internal sealed class PauseReportSaveInterceptor : SaveChangesInterceptor
{
    private int _armed;
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Paused => _paused.Task;
    public void Arm() => Interlocked.Exchange(ref _armed, 1);
    public void Resume() => _resume.TrySetResult();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Report>().Any(e => e.State == EntityState.Modified)
            && Interlocked.CompareExchange(ref _armed, 0, 1) == 1)
        {
            _paused.TrySetResult();
            await _resume.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        return result;
    }
}
