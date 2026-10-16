using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nagorik.Api.Services;

internal static class ResidentNotificationTestHelper
{


    public static ResidentReportStatusNotifier Create(INotificationService? sender = null) =>
        new(sender ?? Mock.Of<INotificationService>(), NullLogger<ResidentReportStatusNotifier>.Instance);
}
