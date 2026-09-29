using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nagorik.Web.Data;
using Nagorik.Web.Models;

public static class TestDb
{
    public static (ApplicationDbContext ctx, SqliteConnection conn) Create()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(conn).Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return (ctx, conn);
    }

    public static ApplicationUser AddUser(ApplicationDbContext ctx, string id = "u1")
    {
        var u = new ApplicationUser { Id = id, UserName = id + "@t.com", Email = id + "@t.com" };
        ctx.Users.Add(u); ctx.SaveChanges(); return u;
    }
}