using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class UtilityAnomalyModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        static string Names<T>() where T : struct, Enum =>
            string.Join(", ", Enum.GetNames<T>().Select(name => $"'{name}'"));
        var highRisk = string.Join(", ", Enum.GetValues<AnomalyType>()
            .Where(UtilityAnomalyRules.IsHighRisk).Select(type => $"'{type}'"));
        var photoElements = Enumerable.Range(0, UtilityAnomalyRules.MaxPhotoCount).Select(i =>
            $"(json_type(\"PhotoPaths\", '$[{i}]') IS NULL OR " +
            $"(json_type(\"PhotoPaths\", '$[{i}]') = 'text' AND " +
            $"length(trim(json_extract(\"PhotoPaths\", '$[{i}]'), char(9) || char(10) || char(13) || ' ')) > 0))");
        builder.Entity<Report>(e =>
        {
            e.Property(r => r.AnomalyType).HasConversion<string>();
            e.Property(r => r.Utility).HasConversion<string>();
            e.Property(r => r.ReferenceNo).HasMaxLength(UtilityAnomalyRules.MaxReferenceLength);
            e.PrimitiveCollection(r => r.PhotoPaths).IsRequired().HasColumnType("TEXT").HasDefaultValueSql("'[]'");
            e.HasIndex(r => r.ReferenceNo).IsUnique().HasFilter("\"ReferenceNo\" IS NOT NULL");
            e.HasIndex(r => new { r.AnomalyType, r.Latitude, r.Longitude, r.CreatedAt });
            e.HasIndex(r => new { r.Utility, r.CreatedAt, r.Id }).IsDescending(false, true, true);
            e.ToTable("Reports", table =>
            {
                table.HasCheckConstraint("CK_Reports_AnomalyType", $"\"AnomalyType\" IS NULL OR \"AnomalyType\" IN ({Names<AnomalyType>()})");
                table.HasCheckConstraint("CK_Reports_Utility", $"\"Utility\" IS NULL OR \"Utility\" IN ({Names<UtilityProvider>()})");
                table.HasCheckConstraint("CK_Reports_ReferenceNo",
                    $"\"ReferenceNo\" IS NULL OR (length(\"ReferenceNo\") BETWEEN 9 AND {UtilityAnomalyRules.MaxReferenceLength} " +
                    "AND substr(\"ReferenceNo\", 1, 3) = 'UA-' AND substr(\"ReferenceNo\", 4) NOT GLOB '*[^0-9]*')");
                table.HasCheckConstraint("CK_Reports_HighRiskUrgency",
                    $"\"AnomalyType\" IS NULL OR \"AnomalyType\" NOT IN ({highRisk}) OR \"Urgency\" IN (2, 3)");
                table.HasCheckConstraint("CK_Reports_PhotoPaths",
                    "CASE WHEN json_valid(\"PhotoPaths\") THEN " +
                    $"json_type(\"PhotoPaths\") = 'array' AND json_array_length(\"PhotoPaths\") <= {UtilityAnomalyRules.MaxPhotoCount} AND " +
                    string.Join(" AND ", photoElements) + " ELSE 0 END");
            });
        });
    }
}
