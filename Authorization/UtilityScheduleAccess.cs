using System.Security.Claims;

namespace Nagorik.Api.Authorization;

public static class UtilityScheduleAccess
{
    public const string Roles = "UtilityOfficial,DPDCOfficial,DESCOOfficial";
    public static bool IsOfficial(ClaimsPrincipal user) =>
        user.IsInRole("UtilityOfficial") || user.IsInRole("DPDCOfficial") || user.IsInRole("DESCOOfficial");
}
