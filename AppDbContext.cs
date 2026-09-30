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

        public DbSet<Report> Reports => Set<Report>();


        // T-002.1 Notification schema
        public DbSet<AreaSubscription> AreaSubscriptions { get; set; }

        public DbSet<NotificationPreference> NotificationPreferences { get; set; }

        public DbSet<UserPushSubscription> UserPushSubscriptions { get; set; }

        public DbSet<NotificationLog> NotificationLogs { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);


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
                    .HasConversion<string>();

                e.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId);
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
        }
    }
}