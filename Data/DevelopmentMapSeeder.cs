using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

public static class DevelopmentMapSeeder
{
    public static async Task SeedAsync(AppDbContext db, IHostEnvironment environment,
        IConfiguration configuration, CancellationToken ct = default)
    {
        // Never populate an operational map with synthetic boundaries in Production.
        if (!environment.IsDevelopment() || !configuration.GetValue("DevelopmentData:SeedMapSamples", true)) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var samples = new[]
        {
            ("Z1", "Dhanmondi", 23.7461, 90.3742, ZoneRiskLevel.High),
            ("Z2", "Mirpur", 23.8223, 90.3654, ZoneRiskLevel.Medium),
            ("Z3", "Gulshan", 23.7925, 90.4078, ZoneRiskLevel.Low)
        };
        foreach (var (code, name, lat, lon, risk) in samples)
        {
            // A pre-existing operational zone is never overwritten or given fake points.
            if (await db.Zones.AnyAsync(z => z.Code == code, ct)) continue;
            var zone = new Zone
            {
                Code = code, Name = name + " (sample boundary)", RiskLevel = risk,
                // Closed counter-clockwise rectangle, standard GeoJSON lon/lat order.
                BoundaryGeoJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "Polygon", coordinates = new[] { new[]
                    {
                        new[] { lon - .003, lat - .003 }, new[] { lon + .003, lat - .003 },
                        new[] { lon + .003, lat + .003 }, new[] { lon - .003, lat + .003 },
                        new[] { lon - .003, lat - .003 }
                    } }
                })
                // No computed timestamp: these are sample levels, not a prediction.
            };
            db.Zones.Add(zone);
            db.DrainagePoints.AddRange(
                new DrainagePoint { Zone = zone, Latitude = lat, Longitude = lon, Status = DrainageStatus.Clear },
                new DrainagePoint { Zone = zone, Latitude = lat + .001, Longitude = lon + .001, Status = DrainageStatus.Blocked });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
