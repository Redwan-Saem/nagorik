using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nagorik.Api;
using Nagorik.Api.Models;

public class SosSchemaTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Alert_SavesGpsOrManualAddressWithOwnerAndOptionalResponder(bool gps)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db, 1);
        TestDb.AddUser(db, 2);
        var alert = new SosAlert
        {
            UserId = 1, Latitude = gps ? 23.81 : null, Longitude = gps ? 90.41 : null,
            AddressText = gps ? "" : "House 5, Dhanmondi, Dhaka", ResponderUserId = gps ? 2 : null
        };
        db.SosAlerts.Add(alert);
        await db.SaveChangesAsync();
        Assert.True(alert.Id > 0);
        db.ChangeTracker.Clear();
        var saved = await db.SosAlerts.Include(a => a.User).Include(a => a.ResponderUser).SingleAsync();
        Assert.Equal(1, saved.User!.Id);
        Assert.Equal(SosAlertStatus.Active, saved.Status);
        Assert.Equal(gps ? 2 : (int?)null, saved.ResponderUser?.Id);
        Assert.Equal(alert.Latitude, saved.Latitude);
        Assert.Equal(alert.AddressText, saved.AddressText);
        Assert.Equal(alert.CreatedAtUtc, saved.CreatedAtUtc);
        Assert.Equal(alert.UpdatedAtUtc, saved.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null, null, "")]
    [InlineData(null, null, " \t\r\n ")]
    [InlineData(23.8, null, "Dhaka")]
    [InlineData(null, 90.4, "Dhaka")]
    [InlineData(91.0, 90.4, "Dhaka")]
    [InlineData(-91.0, 90.4, "Dhaka")]
    [InlineData(23.8, 181.0, "Dhaka")]
    [InlineData(23.8, -181.0, "Dhaka")]
    public async Task Alert_DatabaseRejectsInvalidLocation(double? lat, double? lon, string address)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        db.SosAlerts.Add(new SosAlert { UserId = 1, Latitude = lat, Longitude = lon, AddressText = address });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.SosAlerts.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(SosAlertStatus.Active, SosAlertStatus.Active)]
    [InlineData(SosAlertStatus.Active, SosAlertStatus.Responding)]
    [InlineData(SosAlertStatus.Responding, SosAlertStatus.Active)]
    [InlineData(SosAlertStatus.Responding, SosAlertStatus.Responding)]
    public async Task Alert_RejectsSecondActiveAlertForSameUser(SosAlertStatus first, SosAlertStatus second)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        db.SosAlerts.Add(new SosAlert { UserId = 1, AddressText = "Dhaka", Status = first });
        await db.SaveChangesAsync();
        db.SosAlerts.Add(new SosAlert { UserId = 1, AddressText = "Dhaka", Status = second });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Single(await db.SosAlerts.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(SosAlertStatus.Completed)]
    [InlineData(SosAlertStatus.Cancelled)]
    public async Task TerminalAlertsAndEventsRemainWhenUserTriggersNewAlert(SosAlertStatus terminal)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        var alert = new SosAlert { UserId = 1, AddressText = "Original address" };
        var history = new SosAlertEvent
        {
            SosAlert = alert, ActorUserId = 1, AddressText = "Original address",
            EventType = SosAlertEventType.Triggered, Status = SosAlertStatus.Active
        };
        db.SosAlertEvents.Add(history);
        await db.SaveChangesAsync();
        alert.Status = terminal;
        alert.AddressText = "Updated address";
        alert.UpdatedAtUtc = DateTime.UtcNow;
        db.SosAlertEvents.Add(new SosAlertEvent
        {
            SosAlertId = alert.Id, ActorUserId = 1, AddressText = alert.AddressText, Status = terminal,
            EventType = terminal == SosAlertStatus.Completed ? SosAlertEventType.Completed : SosAlertEventType.Cancelled
        });
        await db.SaveChangesAsync();
        db.SosAlerts.Add(new SosAlert { UserId = 1, AddressText = "New emergency location" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.SosAlerts.CountAsync());
        Assert.Equal(terminal, (await db.SosAlerts.SingleAsync(a => a.Id == alert.Id)).Status);
        var events = await db.SosAlertEvents.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal("Original address", events[0].AddressText);
        Assert.Equal("Updated address", events[1].AddressText);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("responder")]
    [InlineData("status")]
    [InlineData("address")]
    [InlineData("actor")]
    [InlineData("eventType")]
    [InlineData("alert")]
    public async Task DatabaseRejectsInvalidReferencesAndValues(string invalid)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db;
        await using var connectionScope = connection;
        TestDb.AddUser(db);
        var alert = new SosAlert { UserId = 1, AddressText = "Dhaka" };
        if (invalid == "owner") alert.UserId = 999;
        if (invalid == "responder") alert.ResponderUserId = 999;
        if (invalid == "status") alert.Status = (SosAlertStatus)999;
        if (invalid == "address") alert.AddressText = new string('a', 501);
        if (invalid is "actor" or "eventType" or "alert")
        {
            db.SosAlerts.Add(alert);
            await db.SaveChangesAsync();
            db.SosAlertEvents.Add(new SosAlertEvent
            {
                SosAlertId = invalid == "alert" ? 999 : alert.Id,
                ActorUserId = invalid == "actor" ? 999 : 1,
                EventType = invalid == "eventType" ? (SosAlertEventType)999 : SosAlertEventType.Triggered,
                Status = SosAlertStatus.Active, AddressText = "Dhaka"
            });
        }
        else db.SosAlerts.Add(alert);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Migration_PreservesExistingDataAndProtectsAppendOnlyEvents()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261016204049_ProtectReportStatusConcurrency");
        TestDb.AddUser(db);
        await TestDb.AddLegacyReportAsync(db, title: "Existing report");
        await migrator.MigrateAsync();
        Assert.Equal("Existing report", (await db.Reports.SingleAsync()).Title);
        Assert.False(db.Database.HasPendingModelChanges());
        var alert = new SosAlert { UserId = 1, AddressText = "Dhaka" };
        db.SosAlertEvents.Add(new SosAlertEvent
        {
            SosAlert = alert, ActorUserId = 1, AddressText = "Dhaka",
            EventType = SosAlertEventType.Triggered, Status = SosAlertStatus.Active
        });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE SosAlertEvents SET AddressText = 'changed';"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM SosAlertEvents;"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM SosAlerts;"));
        Assert.Equal("Dhaka", (await db.SosAlertEvents.AsNoTracking().SingleAsync()).AddressText);

        await migrator.MigrateAsync("20261016204049_ProtectReportStatusConcurrency");
        await migrator.MigrateAsync();
        Assert.Equal("Existing report", (await db.Reports.AsNoTracking().SingleAsync()).Title);
        Assert.Empty(await db.SosAlerts.AsNoTracking().ToListAsync());
    }
}
