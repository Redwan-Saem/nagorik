using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<WaterloggingRisk> WaterloggingRisks { get; set; }
        public DbSet<ZoneMapData> ZoneMapData { get; set; }
        public DbSet<Report> Reports => Set<Report>();


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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
        }
    }
}