using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class UtilityRoutingModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        var providers = string.Join(", ", Enum.GetValues<UtilityProvider>()
            .Where(p => p != UtilityProvider.Unassigned).Select(p => $"'{p}'"));
        var types = string.Join(", ", Enum.GetNames<AnomalyType>().Select(t => $"'{t}'"));
        builder.Entity<UtilityRoutingRule>(e =>
        {
            e.Property(r => r.Name).IsRequired().HasMaxLength(160);
            e.Property(r => r.Utility).HasConversion<string>().IsRequired();
            e.Property(r => r.AnomalyType).HasConversion<string>();
            e.Property(r => r.IsActive).HasDefaultValue(false);
            e.HasOne(r => r.Area).WithMany().HasForeignKey(r => r.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.Name).IsUnique();
            e.HasIndex(r => new { r.IsActive, r.AnomalyType, r.AreaId, r.Priority })
                .IsDescending(false, false, false, true);
            e.ToTable("UtilityRoutingRules", table =>
            {
                table.HasCheckConstraint("CK_UtilityRoutingRules_Name",
                    "length(\"Name\") <= 160 AND length(trim(\"Name\", char(9) || char(10) || char(13) || ' ')) > 0");
                table.HasCheckConstraint("CK_UtilityRoutingRules_Utility", $"\"Utility\" IN ({providers})");
                table.HasCheckConstraint("CK_UtilityRoutingRules_AnomalyType",
                    $"\"AnomalyType\" IS NULL OR \"AnomalyType\" IN ({types})");
                table.HasCheckConstraint("CK_UtilityRoutingRules_Priority", "\"Priority\" BETWEEN 0 AND 10000");
                table.HasCheckConstraint("CK_UtilityRoutingRules_IsActive", "\"IsActive\" IN (0, 1)");
                table.HasCheckConstraint("CK_UtilityRoutingRules_Bounds", """
                    ("MinLatitude" IS NULL AND "MaxLatitude" IS NULL AND "MinLongitude" IS NULL AND "MaxLongitude" IS NULL)
                    OR ("MinLatitude" IS NOT NULL AND "MaxLatitude" IS NOT NULL AND "MinLongitude" IS NOT NULL AND "MaxLongitude" IS NOT NULL
                        AND "MinLatitude" BETWEEN -90 AND 90 AND "MaxLatitude" BETWEEN -90 AND 90
                        AND "MinLongitude" BETWEEN -180 AND 180 AND "MaxLongitude" BETWEEN -180 AND 180
                        AND "MinLatitude" <= "MaxLatitude" AND "MinLongitude" <= "MaxLongitude")
                    """);
            });
            e.HasData(SeedRules());
        });
    }

    private static IEnumerable<UtilityRoutingRule> SeedRules()
    {
        yield return new UtilityRoutingRule
        {
            Id = 1, Name = "Water leak / generic water authority", AnomalyType = Models.AnomalyType.WaterLeak,
            Utility = UtilityProvider.WaterAuthority, IsActive = true
        };
        yield return new UtilityRoutingRule
        {
            Id = 2, Name = "Gas leak / generic gas authority", AnomalyType = Models.AnomalyType.GasLeak,
            Utility = UtilityProvider.GasAuthority, IsActive = true
        };
        var id = 10;
        foreach (var provider in new[] { UtilityProvider.Desco, UtilityProvider.Dpdc })
        foreach (var (areaId, areaName) in new[] { (1, "Dhanmondi"), (2, "Mirpur"), (3, "Gulshan") })
        foreach (var type in new[] { Models.AnomalyType.ExposedWire, Models.AnomalyType.FallenPole, Models.AnomalyType.PowerOutage })
            yield return new UtilityRoutingRule
            {
                Id = id++, Name = $"{provider.ToString().ToUpperInvariant()} / {areaName} / {type} (unverified template)",
                Utility = provider, AnomalyType = type, AreaId = areaId, Priority = 100, IsActive = false
            };
    }
}
