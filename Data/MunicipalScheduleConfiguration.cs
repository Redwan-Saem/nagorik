using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Data;
public static class MunicipalScheduleConfiguration
{
    public static void Configure(ModelBuilder b)=>b.Entity<MunicipalSchedule>(e=>
    {
        e.HasOne<Zone>().WithMany().HasForeignKey(x=>x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x=>x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        e.Property(x=>x.Revision).IsConcurrencyToken();e.Property(x=>x.Kind).HasMaxLength(10);e.Property(x=>x.State).HasMaxLength(15);
        e.Property(x=>x.Source).IsRequired().HasMaxLength(300);e.Property(x=>x.Notes).HasMaxLength(1000);
        e.HasIndex(x=>new{x.Kind,x.ZoneId,x.State,x.FromDate,x.ToDate});
        e.ToTable("MunicipalSchedules",t=>
        {
            t.HasCheckConstraint("CK_MunicipalSchedule_Values","Kind IN ('Water','Waste') AND State IN ('Draft','Published','Retired') AND Revision > 0 AND DaysMask BETWEEN 1 AND 127 AND StartMinute BETWEEN 0 AND 1439 AND EndMinute > StartMinute AND EndMinute <= 1440 AND ToDate >= FromDate");
            t.HasCheckConstraint("CK_MunicipalSchedule_Publication","State <> 'Published' OR PublishedAtUtc IS NOT NULL");
        });
    });
}
