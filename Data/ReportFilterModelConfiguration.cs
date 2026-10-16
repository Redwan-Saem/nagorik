using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class ReportFilterModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ReportArea>(e =>
        {
            e.Property(a => a.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(a => a.Name).IsUnique();
            e.HasData(new ReportArea { Id = 1, Name = "Dhanmondi" },
                new ReportArea { Id = 2, Name = "Mirpur" }, new ReportArea { Id = 3, Name = "Gulshan" });
        });
        builder.Entity<Report>(e =>
        {
            e.ToTable("Reports", t => t.HasCheckConstraint("CK_Reports_Urgency", "\"Urgency\" IN (0, 1, 2, 3)"));
            // Normal is the database default and EF sentinel, so an explicit Low (0) is preserved.
            e.Property(r => r.Urgency).HasDefaultValue(ReportUrgency.Normal).HasSentinel(ReportUrgency.Normal);
            e.HasOne(r => r.Area).WithMany().HasForeignKey(r => r.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.CreatedAt, r.Id }).IsDescending(true, true);
            e.HasIndex(r => new { r.Category, r.CreatedAt, r.Id }).IsDescending(false, true, true);
            e.HasIndex(r => new { r.AreaId, r.CreatedAt, r.Id }).IsDescending(false, true, true);
            e.HasIndex(r => new { r.AreaId, r.Category, r.CreatedAt, r.Id }).IsDescending(false, false, true, true);
            e.HasIndex(r => new { r.Status, r.CreatedAt, r.Id }).IsDescending(false, true, true);
            e.HasIndex(r => new { r.Urgency, r.CreatedAt, r.Id }).IsDescending(true, true, true);
        });
    }
}
