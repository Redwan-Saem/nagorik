using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<WaterloggingRisk> WaterloggingRisks { get; set; }

        public DbSet<ZoneMapData> ZoneMapData { get; set; }

        public DbSet<Zone> Zones => Set<Zone>();
        public DbSet<DrainagePoint> DrainagePoints => Set<DrainagePoint>();
        public DbSet<RainfallReading> RainfallReadings => Set<RainfallReading>();
        public DbSet<RiskHistory> RiskHistories => Set<RiskHistory>();

        public DbSet<Report> Reports => Set<Report>();
        public DbSet<ReportArea> ReportAreas => Set<ReportArea>();
        public DbSet<UtilityRoutingRule> UtilityRoutingRules => Set<UtilityRoutingRule>();
        public DbSet<SosAlert> SosAlerts => Set<SosAlert>();
        public DbSet<SosAlertEvent> SosAlertEvents => Set<SosAlertEvent>();


        // T-002.1 Notification schema
        public DbSet<AreaSubscription> AreaSubscriptions { get; set; }

        public DbSet<NotificationPreference> NotificationPreferences { get; set; }

        public DbSet<UserPushSubscription> UserPushSubscriptions { get; set; }

        public DbSet<NotificationLog> NotificationLogs { get; set; }


        // T-004.1 Crew assignment schema
        public DbSet<Crew> Crews { get; set; }

        public DbSet<CrewAssignment> CrewAssignments { get; set; }
        public DbSet<ReportStatusHistory> ReportStatusHistories
            => Set<ReportStatusHistory>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            Data.SosModelConfiguration.Configure(builder);
            Data.ReportFilterModelConfiguration.Configure(builder);
            Data.UtilityAnomalyModelConfiguration.Configure(builder);
            Data.UtilityRoutingModelConfiguration.Configure(builder);
            Data.ZoneModelConfiguration.Configure(builder);
            Data.DrainagePointModelConfiguration.Configure(builder);
            Data.RainfallModelConfiguration.Configure(builder);
            builder.Entity<RiskHistory>(e =>
            {
                e.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
                e.Property(x => x.PreviousLevel).HasConversion<string>();
                e.Property(x => x.Level).HasConversion<string>();
                e.Property(x => x.RuleVersion).IsRequired().HasMaxLength(40);
                e.Property(x => x.FactorsJson).IsRequired();
                e.HasIndex(x => new { x.ZoneId, x.ComputedAtUtc });
                e.ToTable("RiskHistories", table =>
                {
                    table.HasCheckConstraint("CK_RiskHistory_Levels", "\"Level\" IN ('Unknown','Low','Medium','High','Severe') AND \"PreviousLevel\" IN ('Unknown','Low','Medium','High','Severe')");
                    table.HasCheckConstraint("CK_RiskHistory_Factors", "json_valid(\"FactorsJson\")");
                });
            });

            builder.Entity<ReportStatusHistory>(entity =>
            {
                entity.Property(x => x.Status)
                    .HasConversion<string>();

                entity.Property(x => x.PreviousStatus)
                    .HasConversion<string>();

                entity.HasOne(x => x.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ChangedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new
                {
                    x.ReportId,
                    x.ChangedAtUtc
                });

                entity.HasOne(x => x.Report)
                    .WithMany()
                    .HasForeignKey(x => x.ReportId);
            });
            // Report configuration
            builder.Entity<Report>(e =>
            {
                e.HasKey(r => r.Id);

                e.Property(r => r.Title)
                    .IsRequired();

                e.Property(r => r.Description)
                    .IsRequired();

                e.Property(r => r.Category)
                    .HasConversion<string>();

                e.Property(r => r.Authority)
                    .HasConversion<string>();

                e.Property(r => r.Status)
                    .HasConversion<string>()
                    .IsConcurrencyToken();

                e.Property(r => r.AssignedCrewId)
                    .IsConcurrencyToken();

                e.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId);

                // T-004.1 Assigned crew relationship
                e.HasOne(r => r.AssignedCrew)
                    .WithMany()
                    .HasForeignKey(r => r.AssignedCrewId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            // AreaSubscription
            builder.Entity<AreaSubscription>()
                .HasIndex(x => new { x.UserId, x.ZoneId })
                .IsUnique();


            // NotificationPreference
            builder.Entity<NotificationPreference>()
                .HasKey(x => x.UserId);


            // UserPushSubscription
            builder.Entity<UserPushSubscription>()
                .Property(x => x.Endpoint)
                .HasMaxLength(500);

            builder.Entity<UserPushSubscription>()
                .HasIndex(x => x.Endpoint)
                .IsUnique();


            // NotificationLog
            builder.Entity<NotificationLog>()
                .HasIndex(x => new { x.UserId, x.SentAtUtc });


            // T-004.1 Crew
            builder.Entity<Crew>()
                .HasIndex(c => c.UserId)
                .IsUnique();

            builder.Entity<Crew>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId);


            // T-004.1 CrewAssignment
            builder.Entity<CrewAssignment>()
                .HasIndex(a => new { a.ReportId, a.AssignedAtUtc });

            builder.Entity<CrewAssignment>()
                .HasOne(a => a.Report)
                .WithMany()
                .HasForeignKey(a => a.ReportId);

            builder.Entity<CrewAssignment>()
                .HasOne(a => a.Crew)
                .WithMany()
                .HasForeignKey(a => a.CrewId);
        }
    }
}
