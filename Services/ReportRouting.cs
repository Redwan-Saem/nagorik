using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public static class ReportRouting
{
    public static Authority GetAuthority(ReportCategory c) =>
        c == ReportCategory.PowerOutage
            ? Authority.DpdcDesco
            : Authority.CityCorporation;
}