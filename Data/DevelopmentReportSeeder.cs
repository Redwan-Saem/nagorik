using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Data;

public static class DevelopmentReportSeeder
{
    public const int SampleCount = 84;
    public const int PerformanceCount = 10_000;

    public static async Task<int> SeedAsync(AppDbContext db, IHostEnvironment environment,
        IConfiguration configuration, CancellationToken ct = default)
    {
        if (!environment.IsDevelopment()) return 0;
        var samples = configuration.GetValue<bool>("DevelopmentData:SeedSampleReports");
        var performance = configuration.GetValue<bool>("DevelopmentData:SeedPerformanceReports");
        if (!samples && !performance) return 0;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        async Task<User> GetUser(string email, string name, string role)
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (user != null)
            {
                if (user.Role != role) throw new InvalidOperationException("Development fixture account has an unexpected role.");
                return user;
            }
            user = new User
            {
                Email = email, Name = name, Role = role, PhoneNumber = "",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"))
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return user;
        }
        var owner = await GetUser("report-fixtures@nagorik.invalid", "Development report fixtures", "Resident");
        var crewUser = await GetUser("report-fixture-crew@nagorik.invalid", "Development fixture crew", "Crew");
        var crew = await db.Crews.SingleOrDefaultAsync(c => c.UserId == crewUser.Id, ct);
        if (crew == null)
        {
            crew = new Crew { Name = "Development fixture crew", UserId = crewUser.Id };
            db.Crews.Add(crew);
            await db.SaveChangesAsync(ct);
        }
        var areas = await db.ReportAreas.OrderBy(a => a.Id).ToListAsync(ct);
        if (areas.Count == 0) throw new InvalidOperationException("Report area lookups must be migrated before seeding reports.");
        var existing = (await db.Reports.Where(r => r.UserId == owner.Id).Select(r => r.Title).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        // Utility reports require their own metadata, reference and private photos.
        var categories = Enum.GetValues<ReportCategory>().Where(c => c != ReportCategory.UtilityAnomaly).ToArray();
        var statuses = Enum.GetValues<ReportStatus>();
        var urgencies = Enum.GetValues<ReportUrgency>();
        var added = 0;
        void AddSet(string prefix, int count)
        {
            var start = DateTime.UtcNow.Date.AddMinutes(-count);
            for (var i = 0; i < count; i++)
            {
                var category = categories[i % categories.Length];
                var area = areas[(i / categories.Length) % areas.Count];
                var status = statuses[i % statuses.Length];
                var urgency = urgencies[(i / (categories.Length * areas.Count)) % urgencies.Length];
                var title = $"{prefix}-{i + 1:D5}: {category} in {area.Name}";
                if (!existing.Add(title)) continue;
                var center = area.Id switch { 1 => (23.7461, 90.3742), 2 => (23.8223, 90.3654), _ => (23.7925, 90.4078) };
                db.Reports.Add(new Report
                {
                    UserId = owner.Id, Title = title, Description = $"Synthetic development {category} report in {area.Name}.",
                    Category = category, Authority = ReportRouting.GetAuthority(category), AreaId = area.Id,
                    Urgency = urgency, Status = status, Location = $"Development sample location, {area.Name}, Dhaka",
                    Latitude = center.Item1 + (i % 10) * 0.00001, Longitude = center.Item2 + (i % 10) * 0.00001,
                    AssignedCrewId = status is ReportStatus.Assigned or ReportStatus.InProgress or ReportStatus.Completed ? crew.Id : null,
                    CreatedAt = start.AddMinutes(i)
                });
                added++;
            }
        }
        if (samples) AddSet("DEV-SAMPLE", SampleCount);
        if (performance) AddSet("DEV-PERF", PerformanceCount);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return added;
    }
}
