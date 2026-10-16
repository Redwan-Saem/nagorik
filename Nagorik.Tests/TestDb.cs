using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
using Nagorik.Api.Models;

public static class TestDb
{
    public static (AppDbContext ctx, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;

        var ctx = new AppDbContext(options);

        ctx.Database.EnsureCreated();

        return (ctx, conn);
    }

    public static User AddUser(AppDbContext ctx, int id = 1)
    {
        var user = new User
        {
            Id = id,
            Name = "Test User",
            Email = $"user{id}@test.com",
            PhoneNumber = "01700000000",
            PasswordHash = "test",
            Role = "Resident"
        };

        ctx.Users.Add(user);
        ctx.SaveChanges();

        return user;
    }

    // Migration fixtures must use the old table contract rather than the current EF model.
    public static Task<int> AddLegacyReportAsync(AppDbContext db, int id = 1, int userId = 1, string title = "Legacy report") =>
        db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Reports (Id, UserId, Title, Description, Category, Authority, Status, Location, CreatedAt, Latitude, Longitude, IsPossibleDuplicate) VALUES ({id}, {userId}, {title}, 'Legacy description', 'RoadDamage', 'CityCorporation', 'Pending', 'Dhaka', {DateTime.UtcNow}, 23.75, 90.39, 0);");
}
