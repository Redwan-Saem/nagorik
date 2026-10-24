using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Data;
public static class CivicCaseConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<CivicCase>(e=>
        {
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.ResidentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x=>new{x.ResidentId,x.Kind,x.SubmissionToken}).IsUnique();e.HasIndex(x=>new{x.Kind,x.State,x.CreatedAtUtc});e.HasIndex(x=>new{x.ResidentId,x.CreatedAtUtc});
            e.Property(x=>x.Revision).IsConcurrencyToken();e.Property(x=>x.Kind).HasMaxLength(20);e.Property(x=>x.Subtype).HasMaxLength(40);e.Property(x=>x.Description).HasMaxLength(1500);e.Property(x=>x.Address).HasMaxLength(300);e.Property(x=>x.SubmissionToken).HasMaxLength(36);e.Property(x=>x.Fingerprint).HasMaxLength(64);e.Property(x=>x.State).HasMaxLength(20);e.Property(x=>x.Reference).HasMaxLength(100);
            e.ToTable("CivicCases",t=>t.HasCheckConstraint("CK_CivicCases_Values","Kind IN ('Noise','Construction') AND State IN ('Pending','UnderReview','Resolved','Rejected') AND Revision > 0 AND Latitude BETWEEN 23.65 AND 23.95 AND Longitude BETWEEN 90.30 AND 90.55 AND (ReportedDecibels IS NULL OR (Kind = 'Noise' AND ReportedDecibels BETWEEN 0 AND 180))"));
        });
        b.Entity<CivicCaseReview>(e=>
        {
            e.HasOne<CivicCase>().WithMany().HasForeignKey(x=>x.CaseId).OnDelete(DeleteBehavior.Restrict);e.HasOne<User>().WithMany().HasForeignKey(x=>x.OfficialId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x=>x.Note).IsRequired().HasMaxLength(1000);e.Property(x=>x.State).HasMaxLength(20);e.HasIndex(x=>new{x.CaseId,x.AtUtc});
        });
    }
}
