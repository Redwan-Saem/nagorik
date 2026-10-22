using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class DrainagePointModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<DrainagePoint>(e =>
        {
            e.ToTable("DrainagePoints", t =>
            {
                t.HasCheckConstraint("CK_DrainagePoints_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
                t.HasCheckConstraint("CK_DrainagePoints_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
                t.HasCheckConstraint("CK_DrainagePoints_Status", "\"Status\" IN ('Unknown', 'Clear', 'Blocked')");
            });
            e.Property(p => p.Status).HasConversion<string>().HasDefaultValue(DrainageStatus.Unknown);
            e.HasOne(p => p.Zone).WithMany().HasForeignKey(p => p.ZoneId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => new { p.ZoneId, p.Status });
        });
    }
}
