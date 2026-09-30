using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public static class DuplicateDetector
{
    public const double RadiusMeters = 50;

    public static async Task<Report?> FindNearbyOpenAsync(
        AppDbContext db,
        ReportCategory cat,
        double lat,
        double lng)
    {
        const double box = 0.0006;

        var candidates = await db.Reports
            .Where(r =>
                r.Category == cat &&
                r.Status != ReportStatus.Completed &&
                r.Latitude > lat - box &&
                r.Latitude < lat + box &&
                r.Longitude > lng - box &&
                r.Longitude < lng + box)
            .OrderBy(r => r.Id)
            .ToListAsync();

        return candidates.FirstOrDefault(r =>
            GeoDistance.Meters(
                lat,
                lng,
                r.Latitude,
                r.Longitude
            ) <= RadiusMeters);
    }
}