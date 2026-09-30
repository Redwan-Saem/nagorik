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
}