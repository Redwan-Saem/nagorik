using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class ZoneModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Zone>(e =>
        {
            e.ToTable("Zones", table =>
            {
                table.HasCheckConstraint("CK_Zones_Code", "length(trim(\"Code\")) BETWEEN 1 AND 40 AND \"Code\" = trim(\"Code\")");
                table.HasCheckConstraint("CK_Zones_Name", "length(trim(\"Name\")) BETWEEN 1 AND 100");
                table.HasCheckConstraint("CK_Zones_RiskLevel", "\"RiskLevel\" IN ('Unknown', 'Low', 'Medium', 'High', 'Severe')");
                // Guard JSON extraction so malformed input fails a constraint, not JSON parsing.
                // Full topology/coordinate validation belongs at the future map write boundary.
                table.HasCheckConstraint("CK_Zones_BoundaryGeoJson", "CASE WHEN json_valid(\"BoundaryGeoJson\") THEN COALESCE(json_extract(\"BoundaryGeoJson\", '$.type') IN ('Polygon', 'MultiPolygon') AND json_type(\"BoundaryGeoJson\", '$.coordinates') = 'array' AND json_array_length(\"BoundaryGeoJson\", '$.coordinates') > 0, 0) ELSE 0 END");
            });
            e.Property(z => z.Code).IsRequired().HasMaxLength(40).UseCollation("NOCASE");
            e.HasIndex(z => z.Code).IsUnique();
            e.Property(z => z.Name).IsRequired().HasMaxLength(100);
            e.Property(z => z.BoundaryGeoJson).IsRequired();
            e.Property(z => z.RiskLevel).HasConversion<string>().HasDefaultValue(ZoneRiskLevel.Unknown);
            e.HasIndex(z => new { z.RiskLevel, z.LastComputedAtUtc });
        });
    }
}
