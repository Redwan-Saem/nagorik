using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Data;

internal static class LoadsheddingModelConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<LoadsheddingSchedule>(e =>
        {
            e.Property(x => x.State).HasConversion<string>();
            e.Property(x => x.Revision).IsConcurrencyToken();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.State, x.StartDate, x.EndDate });
            e.ToTable("LoadsheddingSchedules", t =>
            {
                t.HasCheckConstraint("CK_LoadsheddingSchedule_Input", "\"AvailableCapacityMw\" > 0 AND \"AvailableCapacityMw\" <= 1000000 AND \"StartDate\" <= \"EndDate\" AND \"SlotMinutes\" IN (60,120,180,240,360) AND \"Revision\" > 0");
                t.HasCheckConstraint("CK_LoadsheddingSchedule_State", "\"State\" IN ('Draft','Published','Superseded') AND (\"State\" = 'Draft' OR (\"PublishedByUserId\" IS NOT NULL AND \"PublishedAtUtc\" IS NOT NULL))");
            });
        });
        b.Entity<ScheduleZoneDemand>(e =>
        {
            e.HasOne(x => x.Schedule).WithMany(x => x.Demands).HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ScheduleId, x.ZoneId }).IsUnique();
            e.ToTable("ScheduleZoneDemands", t => t.HasCheckConstraint("CK_ScheduleZoneDemand_Positive", "\"DemandMw\" > 0 AND \"DemandMw\" <= 1000000"));
        });
        b.Entity<LoadsheddingSlot>(e =>
        {
            e.HasOne(x => x.Schedule).WithMany(x => x.Slots).HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ScheduleId, x.ZoneId, x.Date, x.StartMinute }).IsUnique();
            e.ToTable("LoadsheddingSlots", t => t.HasCheckConstraint("CK_LoadsheddingSlot_Time", "\"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\""));
        });
        b.Entity<SchedulePublication>(e =>
        {
            e.HasKey(x => new { x.ZoneId, x.Date });
            e.HasOne<Zone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ScheduleId);
        });
        b.Entity<ScheduleAudit>(e =>
        {
            e.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ScheduleId, x.Id });
            e.Property(x => x.Action).HasMaxLength(32).IsRequired();
            e.Property(x => x.Details).HasMaxLength(4000).IsRequired();
        });
    }
}
