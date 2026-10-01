using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public interface ICrewTaskNotifier
{
    Task NotifyAssignedAsync(Report report, Crew crew);
}

public class NullCrewTaskNotifier : ICrewTaskNotifier
{
    public Task NotifyAssignedAsync(Report report, Crew crew)
        => Task.CompletedTask;
}