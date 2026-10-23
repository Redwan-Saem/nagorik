using Microsoft.AspNetCore.Authorization;

namespace Nagorik.Api.Authorization;

public static class DispatcherMapAccess
{
    public const string Policy = "DispatcherMap";

    public static void Configure(AuthorizationOptions options) =>
        options.AddPolicy(Policy, policy => policy.RequireAuthenticatedUser().RequireRole("Dispatcher"));
}
