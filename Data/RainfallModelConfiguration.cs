using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class RainfallModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<RainfallReading>(e =>
        {
            e.ToTable("RainfallReadings", t =>
            {
                t.HasCheckConstraint("CK_RainfallReadings_Amounts", "\"Rainfall1HourMm\" >= 0 AND \"Rainfall3HourMm\" >= \"Rainfall1HourMm\" AND \"Rainfall6HourMm\" >= \"Rainfall3HourMm\" AND \"Rainfall6HourMm\" <= 10000");
                t.HasCheckConstraint("CK_RainfallReadings_Source", "length(trim(\"Source\")) BETWEEN 1 AND 64");
                t.HasCheckConstraint("CK_RainfallReadings_Forecast", "\"IsForecast\" IN (0, 1)");
            });
            e.Property(r => r.Source).IsRequired().HasMaxLength(64);
            e.HasOne(r => r.Zone).WithMany().HasForeignKey(r => r.ZoneId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.ZoneId, r.TimestampUtc, r.IsForecast, r.Source }).IsUnique();
            e.HasIndex(r => new { r.ZoneId, r.IsForecast, r.TimestampUtc }).IsDescending(false, false, true);
        });
    }
}
