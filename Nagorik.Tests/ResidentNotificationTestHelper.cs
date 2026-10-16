using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nagorik.Api.Services;

internal static class ResidentNotificationTestHelper
{
    public static DispatcherTaskStatusNotifier Dispatcher(Nagorik.Api.AppDbContext db, INotificationService? sender = null) =>
        new(db, sender ?? Mock.Of<INotificationService>(), NullLogger<DispatcherTaskStatusNotifier>.Instance);

    public static ResidentReportStatusNotifier Create(INotificationService? sender = null) =>
        new(sender ?? Mock.Of<INotificationService>(), NullLogger<ResidentReportStatusNotifier>.Instance);
}
