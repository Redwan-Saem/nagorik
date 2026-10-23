using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Data;

public static class TransportModelConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<TransportStop>(e =>
        {
            e.Property(x=>x.Name).IsRequired().HasMaxLength(160);
            e.Property(x=>x.Source).IsRequired().HasMaxLength(500);
            e.HasIndex(x=>new{x.Enabled,x.Name});
            e.ToTable("TransportStops",t=>t.HasCheckConstraint("CK_TransportStops_Coordinates","Latitude BETWEEN -90 AND 90 AND Longitude BETWEEN -180 AND 180"));
        });
        b.Entity<TransportLink>(e =>
        {
            e.HasOne<TransportStop>().WithMany().HasForeignKey(x=>x.FromStopId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TransportStop>().WithMany().HasForeignKey(x=>x.ToStopId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x=>new{x.FromStopId,x.Enabled});
            e.Property(x=>x.Mode).IsRequired().HasMaxLength(20);e.Property(x=>x.Line).HasMaxLength(100);e.Property(x=>x.Source).HasMaxLength(500);
            e.ToTable("TransportLinks",t=>t.HasCheckConstraint("CK_TransportLinks_Values","FromStopId <> ToStopId AND DistanceKm > 0 AND DistanceKm <= 100 AND Minutes BETWEEN 1 AND 300 AND Mode IN ('Bus','Metro','CNG','Rickshaw','Walk')"));
        });
        b.Entity<TransportFareRule>(e =>
        {
            e.HasKey(x=>x.Mode);e.Property(x=>x.Mode).HasMaxLength(20);e.Property(x=>x.Source).HasMaxLength(500);
            e.ToTable("TransportFareRules",t=>t.HasCheckConstraint("CK_TransportFareRules_Values","BaseBdt BETWEEN 0 AND 10000 AND PerKmBdt BETWEEN 0 AND 10000 AND MinimumBdt BETWEEN 0 AND 10000 AND UpperMultiplier BETWEEN 1 AND 10 AND Mode IN ('Bus','Metro','CNG','Rickshaw','Walk')"));
        });
    }
}
