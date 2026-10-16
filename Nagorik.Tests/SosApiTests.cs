using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.IdentityModel.Tokens;
using Nagorik.Api;
using Nagorik.Api.Controllers;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class SosApiTests : IAsyncLifetime
{
    private const string Key = "sos-api-tests-signing-key-with-at-least-32-bytes";
    private SqliteConnection _connection = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly PauseSosSave _gate = new();
    private readonly Mock<INotificationService> _notifications = new();
    private static readonly object Gps = new { latitude = 23.81, longitude = 90.41 };

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection).AddInterceptors(_gate));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(SosApiController).Assembly);
        builder.Services.AddScoped<SosAlertService>();
        builder.Services.AddSingleton(_notifications.Object);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
            };
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Token ??= context.Request.Cookies["auth_token"];
                    return Task.CompletedTask;
                }
            };
        });
        builder.Services.AddAuthorization();
        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        _app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await using (var db = CreateDb())
        {
            await db.Database.MigrateAsync();
            TestDb.AddUser(db, 1);
            TestDb.AddUser(db, 2);
        }
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }










    [Fact]
    public async Task Lifecycle_UsesCookieIdentityAndAtomicallyAuditsTriggerLocationAndCancellation()
    {
        Authenticate("1");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync("/api/sos/active")).StatusCode);
        // Identity, status, and responder fields supplied by the caller have no authority.
        using var created = await _client.PostAsJsonAsync("/api/sos", new
        {
            latitude = 23.81, longitude = 90.41, addressText = "  Dhanmondi  ", userId = 2,
            status = "Completed", responderUserId = 2
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var alert = (await created.Content.ReadFromJsonAsync<SosAlertDto>())!;
        Assert.Equal("Active", alert.Status);
        Assert.Equal("Dhanmondi", alert.AddressText);
        Assert.Null(alert.ResponderUserId);
        Assert.Equal(new Uri(_client.BaseAddress!, $"/api/sos/{alert.Id}"), created.Headers.Location);
        using var detail = await _client.GetAsync(created.Headers.Location);
        detail.EnsureSuccessStatusCode();
        Assert.Equal(alert.Id, (await _client.GetFromJsonAsync<SosAlertDto>("/api/sos/active"))!.Id);

        using var duplicate = await _client.PostAsJsonAsync("/api/sos", Gps);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var duplicateJson = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(alert.Id, duplicateJson.RootElement.GetProperty("activeAlertId").GetInt32());
        using var moved = await _client.PatchAsJsonAsync($"/api/sos/{alert.Id}/location", new { latitude = 23.82, longitude = 90.42 });
        moved.EnsureSuccessStatusCode();
        var updated = (await moved.Content.ReadFromJsonAsync<SosAlertDto>())!;
        Assert.True(updated.UpdatedAtUtc > alert.UpdatedAtUtc);
        using var cancel = await _client.PostAsync($"/api/sos/{alert.Id}/cancel", null);
        cancel.EnsureSuccessStatusCode();
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<SosAlertDto>())!.Status);
        using var repeat = await _client.PostAsync($"/api/sos/{alert.Id}/cancel", null);
        repeat.EnsureSuccessStatusCode();
        using var lateUpdate = await _client.PatchAsJsonAsync($"/api/sos/{alert.Id}/location", Gps);
        Assert.Equal(HttpStatusCode.Conflict, lateUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync("/api/sos/active")).StatusCode);

        await using var db = CreateDb();
        Assert.Equal(1, (await db.SosAlerts.SingleAsync()).UserId);
        var events = await db.SosAlertEvents.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(new[] { SosAlertEventType.Triggered, SosAlertEventType.LocationUpdated, SosAlertEventType.Cancelled }, events.Select(e => e.EventType));
        Assert.All(events, e => Assert.Equal(1, e.ActorUserId));
        Assert.Equal(23.81, events[0].Latitude);
        Assert.Equal(23.82, events[1].Latitude);
        Assert.Equal(SosAlertStatus.Cancelled, events[2].Status);
        Assert.Equal(updated.UpdatedAtUtc, events[1].OccurredAtUtc);
        using var next = await _client.PostAsJsonAsync("/api/sos", new { addressText = "Dhaka, House 10" });
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Assert.Equal(2, await db.SosAlerts.CountAsync());
        Assert.Equal(4, await db.SosAlertEvents.CountAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"addressText\":\" \\t\\r\\n \"}")]
    [InlineData("{\"latitude\":23.8}")]
    [InlineData("{\"longitude\":90.4,\"addressText\":\"Dhaka\"}")]
    [InlineData("{\"latitude\":91,\"longitude\":90}")]
    [InlineData("{\"latitude\":23,\"longitude\":-181}")]
    [InlineData("{\"latitude\":\"NaN\",\"longitude\":90}")]
    [InlineData("{\"latitude\":\"Infinity\",\"longitude\":90}")]
    [InlineData("{\"latitude\":true,\"longitude\":90}")]
    [InlineData("{\"addressText\":[]}")]
    [InlineData("{\"addressText\":\"Dhaka\"")]
    public async Task InvalidRequests_Return400AndMakeNoWrites(string json)
    {
        Authenticate("1");
        using var response = await _client.PostAsync("/api/sos", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = CreateDb();
        Assert.Empty(await db.SosAlerts.ToListAsync());
        Assert.Empty(await db.SosAlertEvents.ToListAsync());
    }

    [Fact]
    public async Task InvalidLocationUpdate_DoesNotChangeAlertOrHistory()
    {
        var alert = await Trigger();
        using var response = await _client.PatchAsJsonAsync($"/api/sos/{alert.Id}/location", new { addressText = new string('a', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = CreateDb();
        Assert.Equal(alert.UpdatedAtUtc, (await db.SosAlerts.SingleAsync()).UpdatedAtUtc);
        Assert.Single(await db.SosAlertEvents.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("0")]
    public async Task AllEndpoints_RequireValidAuthentication(string? identity)
    {
        if (identity != null) Authenticate(identity);
        using var trigger = await _client.PostAsJsonAsync("/api/sos", Gps);
        using var active = await _client.GetAsync("/api/sos/active");
        using var detail = await _client.GetAsync("/api/sos/1");
        using var location = await _client.PatchAsJsonAsync("/api/sos/1/location", Gps);
        using var cancel = await _client.PostAsync("/api/sos/1/cancel", null);
        foreach (var response in new[] { trigger, active, detail, location, cancel })
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var db = CreateDb();
        Assert.Empty(await db.SosAlerts.ToListAsync());
    }

    [Fact]
    public async Task OtherUserAndMissingAlert_AreHiddenWithoutWrites()
    {
        var alert = await Trigger();
        Authenticate("2");
        foreach (var id in new[] { alert.Id, 999 })
        {
            using var detail = await _client.GetAsync($"/api/sos/{id}");
            using var location = await _client.PatchAsJsonAsync($"/api/sos/{id}/location", Gps);
            using var cancel = await _client.PostAsync($"/api/sos/{id}/cancel", null);
            foreach (var response in new[] { detail, location, cancel }) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync("/api/sos/active")).StatusCode);
        using var own = await _client.PostAsJsonAsync("/api/sos", new { addressText = "My address" });
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
        await using var db = CreateDb();
        Assert.Equal(2, await db.SosAlerts.CountAsync());
        Assert.Equal(2, await db.SosAlertEvents.CountAsync());
    }

    [Theory]
    [InlineData(SosAlertStatus.Responding)]
    [InlineData(SosAlertStatus.Completed)]
    public async Task ExistingStatus_IsRespectedByTriggerUpdateAndCancel(SosAlertStatus status)
    {
        var alert = await Trigger();
        await using (var db = CreateDb())
            await db.SosAlerts.Where(a => a.Id == alert.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, status));
        using var location = await _client.PatchAsJsonAsync($"/api/sos/{alert.Id}/location", new { addressText = "New address" });
        Assert.Equal(status == SosAlertStatus.Responding ? HttpStatusCode.OK : HttpStatusCode.Conflict, location.StatusCode);
        using var duplicate = await _client.PostAsJsonAsync("/api/sos", Gps);
        Assert.Equal(status == SosAlertStatus.Responding ? HttpStatusCode.Conflict : HttpStatusCode.Created, duplicate.StatusCode);
        using var cancel = await _client.PostAsync($"/api/sos/{alert.Id}/cancel", null);
        Assert.Equal(status == SosAlertStatus.Responding ? HttpStatusCode.OK : HttpStatusCode.Conflict, cancel.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveFailure_DoesNotLeavePartialAlertOrEvent(bool updating)
    {
        SosAlertDto? alert = updating ? await Trigger() : null;
        Authenticate("1");
        await using (var db = CreateDb())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_sos_event BEFORE INSERT ON SosAlertEvents BEGIN SELECT RAISE(ABORT, 'test save failure'); END;");
        using var response = updating
            ? await _client.PatchAsJsonAsync($"/api/sos/{alert!.Id}/location", new { addressText = "New location" })
            : await _client.PostAsJsonAsync("/api/sos", Gps);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var persisted = CreateDb();
        Assert.Equal(updating ? 1 : 0, await persisted.SosAlerts.CountAsync());
        Assert.Equal(updating ? 1 : 0, await persisted.SosAlertEvents.CountAsync());
        if (updating) Assert.Equal(alert!.UpdatedAtUtc, (await persisted.SosAlerts.SingleAsync()).UpdatedAtUtc);
    }

    [Fact]
    public async Task ConcurrentTriggers_CreateOneAlertAndOneEvent()
    {
        Authenticate("1");
        _gate.Arm();
        var first = _client.PostAsJsonAsync("/api/sos", Gps);
        try
        {
            await _gate.Paused.WaitAsync(TimeSpan.FromSeconds(10));
            using var winner = await _client.PostAsJsonAsync("/api/sos", Gps);
            Assert.Equal(HttpStatusCode.Created, winner.StatusCode);
        }
        finally { _gate.Resume(); }
        using var loser = await first;
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        await using var db = CreateDb();
        Assert.Single(await db.SosAlerts.ToListAsync());
        Assert.Single(await db.SosAlertEvents.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentLocationAndCancellation_RejectStaleChangeWithoutExtraEvent(bool cancelWins)
    {
        var alert = await Trigger();
        _gate.Arm();
        var first = cancelWins
            ? _client.PatchAsJsonAsync($"/api/sos/{alert.Id}/location", new { addressText = "Stale location" })
            : _client.PostAsync($"/api/sos/{alert.Id}/cancel", null);
        try
        {
            await _gate.Paused.WaitAsync(TimeSpan.FromSeconds(10));
            using var winner = cancelWins
                ? await _client.PostAsync($"/api/sos/{alert.Id}/cancel", null)
                : await _client.PatchAsJsonAsync($"/api/sos/{alert.Id}/location", new { addressText = "Current location" });
            winner.EnsureSuccessStatusCode();
        }
        finally { _gate.Resume(); }
        using var loser = await first;
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        await using var db = CreateDb();
        var saved = await db.SosAlerts.SingleAsync();
        Assert.Equal(cancelWins ? SosAlertStatus.Cancelled : SosAlertStatus.Active, saved.Status);
        Assert.Equal(cancelWins ? "" : "Current location", saved.AddressText);
        Assert.Equal(2, await db.SosAlertEvents.CountAsync());
    }

    [Fact]
    public async Task RepeatedLocation_DoesNotDuplicateAuditEvent()
    {
        var alert = await Trigger();
        using var response = await _client.PatchAsJsonAsync($"/api/sos/{alert.Id}/location", Gps);
        response.EnsureSuccessStatusCode();
        await using var db = CreateDb();
        Assert.Single(await db.SosAlertEvents.ToListAsync());
    }

    [Fact]
    public async Task DeletedAccountCannotTriggerAlert()
    {
        Authenticate("999");
        using var response = await _client.PostAsJsonAsync("/api/sos", Gps);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<SosAlertDto> Trigger()
    {
        Authenticate("1");
        using var response = await _client.PostAsJsonAsync("/api/sos", Gps);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SosAlertDto>())!;
    }

    private AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    private void Authenticate(string id)
    {
        var token = new JwtSecurityToken(claims: new[] { new Claim(ClaimTypes.NameIdentifier, id) },
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials:
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", $"auth_token={new JwtSecurityTokenHandler().WriteToken(token)}");
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null) await _app.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    private sealed class PauseSosSave : SaveChangesInterceptor
    {
        private int _armed;
        private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Paused => _paused.Task;
        public void Arm() => Interlocked.Exchange(ref _armed, 1);
        public void Resume() => _resume.TrySetResult();
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SosAlert>().Any(e => e.State is EntityState.Added or EntityState.Modified)
                && Interlocked.CompareExchange(ref _armed, 0, 1) == 1)
            {
                _paused.TrySetResult();
                await _resume.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
    }

}
