using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
using Nagorik.Api.Models;
using Xunit;

namespace Nagorik.Tests;

public class ReportSchemaTests
{
    // In-memory SQLite. Connection must be opened BEFORE EnsureCreated(),
    // otherwise the in-memory DB vanishes and foreign keys are not enforced.
    private static AppDbContext CreateDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    // Adjust these two helpers to match your real models.
    private static User MakeUser() => new()
    {
        // e.g. Username = "test", Email = "t@t.com", PasswordHash = "x"
    };

    private static Report MakeReport(int userId) => new()
    {
        UserId = userId,
        // e.g. Title = "Broken drain", Description = "Near Dhanmondi 27"
    };

    [Fact]
    public void Report_WithValidUser_IsSaved()
    {
        using var db = CreateDb();
        var user = MakeUser();
        db.Set<User>().Add(user);
        db.SaveChanges();

        db.Set<Report>().Add(MakeReport(user.Id));
        db.SaveChanges();

        Assert.Equal(1, db.Set<Report>().Count());
    }

    [Fact]
    public void Report_WithUnknownUser_IsRejected()
    {
        using var db = CreateDb();

        db.Set<Report>().Add(MakeReport(userId: 9999));

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Report_UserNavigation_LoadsOwner()
    {
        using var db = CreateDb();
        var user = MakeUser();
        db.Set<User>().Add(user);
        db.SaveChanges();
        db.Set<Report>().Add(MakeReport(user.Id));
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var report = db.Set<Report>().Include(r => r.User).Single();

        Assert.NotNull(report.User);
        Assert.Equal(user.Id, report.User!.Id);
    }
}