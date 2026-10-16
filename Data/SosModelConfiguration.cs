using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class SosModelConfiguration
{
    private const string ValidStatus = "\"Status\" IN ('Active', 'Responding', 'Completed', 'Cancelled')";
    private const string ValidCoordinates = "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)";
    private const string HasLocation = "(\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL) OR length(trim(\"AddressText\", char(9) || char(10) || char(13) || ' ')) > 0";

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<SosAlert>(e =>
        {
            e.ToTable("SosAlerts", t =>
            {
                t.HasCheckConstraint("CK_SosAlerts_Status", ValidStatus);
                t.HasCheckConstraint("CK_SosAlerts_Coordinates", ValidCoordinates);
                t.HasCheckConstraint("CK_SosAlerts_Location", HasLocation);
                t.HasCheckConstraint("CK_SosAlerts_AddressLength", "length(\"AddressText\") <= 500");
            });
            e.Property(a => a.AddressText).IsRequired().HasMaxLength(500);
            e.Property(a => a.Status).HasConversion<string>().IsConcurrencyToken();
            e.Property(a => a.UpdatedAtUtc).IsConcurrencyToken();
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.ResponderUser).WithMany().HasForeignKey(a => a.ResponderUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => a.UserId).IsUnique().HasDatabaseName("IX_SosAlerts_OneActivePerUser")
                .HasFilter("\"Status\" IN ('Active', 'Responding')");
            e.HasIndex(a => new { a.UserId, a.CreatedAtUtc });
            e.HasIndex(a => new { a.Status, a.CreatedAtUtc });
        });

        builder.Entity<SosAlertEvent>(e =>
        {
            e.ToTable("SosAlertEvents", t =>
            {
                t.HasCheckConstraint("CK_SosAlertEvents_Status", ValidStatus);
                t.HasCheckConstraint("CK_SosAlertEvents_Coordinates", ValidCoordinates);
                t.HasCheckConstraint("CK_SosAlertEvents_Location", HasLocation);
                t.HasCheckConstraint("CK_SosAlertEvents_AddressLength", "length(\"AddressText\") <= 500");
                t.HasCheckConstraint("CK_SosAlertEvents_EventType",
                    "\"EventType\" IN ('Triggered', 'LocationUpdated', 'ResponderAssigned', 'Completed', 'Cancelled')");
            });
            e.Property(a => a.AddressText).IsRequired().HasMaxLength(500);
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.EventType).HasConversion<string>();
            e.HasOne(a => a.SosAlert).WithMany().HasForeignKey(a => a.SosAlertId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.ActorUser).WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.SosAlertId, a.OccurredAtUtc });
        });
    }
}
