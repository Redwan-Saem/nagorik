namespace Nagorik.Api.Authorization;

public static class OfficialAccess
{
    // Official is the existing role; CityOfficial is the backlog-compatible alias.
    public const string Roles = "Official,CityOfficial";
    public static bool IsOfficial(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole("Official") || user.IsInRole("CityOfficial");
}
