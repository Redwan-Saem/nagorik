using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nagorik.Api;
using Nagorik.Api.Controllers;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public sealed class BackupPowerApiTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nagorik-backup-api-" + Guid.NewGuid().ToString("N"));
    private WebApplication app = null!;
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = "Development" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(BackupPowerController).Assembly);
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(root, "test.db")};Pooling=False"));
        builder.Services.AddSingleton<BackupPowerSolver>(); builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ResidentScheduleQueryService>();
        builder.Services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, BackupFixtureAuth>("fixture", _ => { });
        builder.Services.AddAuthorization(); app = builder.Build();
        _ = app.Services.GetRequiredService<BackupPowerSolver>();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
            await MunicipalScheduleTests.Seed(db);
            db.Users.AddRange(new User { Id = 9001, Name = "DPDC fixture", Email = "dpdc@backup.invalid", Role = "DPDCOfficial", PasswordHash = "fixture" },
                new User { Id = 9002, Name = "Resident fixture", Email = "resident@backup.invalid", Role = "Resident", PasswordHash = "fixture" },
                new User { Id = 9003, Name = "Crew fixture", Email = "crew@backup.invalid", Role = "Crew", PasswordHash = "fixture" });
            await db.SaveChangesAsync();
            var today = DateOnly.FromDateTime(DhakaTime.ToLocal(DateTime.UtcNow));
            var schedule = new LoadsheddingSchedule { StartDate = today, EndDate = today, AvailableCapacityMw = 10, SlotMinutes = 60,
                CreatedByUserId = 9001, CreatedAtUtc = DateTime.UtcNow, State = ScheduleState.Published, PublishedAtUtc = DateTime.UtcNow, PublishedByUserId = 9001,
                Slots = Enumerable.Range(12, 3).Select(h => new LoadsheddingSlot { ZoneId = 1, Date = today, StartMinute = h * 60, EndMinute = (h + 1) * 60 }).ToList() };
            db.LoadsheddingSchedules.Add(schedule); await db.SaveChangesAsync();
            db.SchedulePublications.Add(new() { ZoneId = 1, Date = today, ScheduleId = schedule.Id }); await db.SaveChangesAsync();
        }
        await app.StartAsync();
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    public sealed class BackupFixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString(); var id = Request.Headers["X-Test-Id"].ToString();
            if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new(ClaimTypes.Role, role), new(ClaimTypes.NameIdentifier, id)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    [Fact]
    public async Task PublicApiReturnsVerifiedSnakeCaseWorkedExampleWithoutAuthentication()
    {
        using var response = await client.PostAsJsonAsync("/api/roster", BackupPowerSolverTests.Worked());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var r = json.RootElement;
        Assert.Equal("ok", r.GetProperty("status").GetString()); Assert.Equal(84, r.GetProperty("value_total").GetInt64());
        Assert.True(r.GetProperty("optimality_proven").GetBoolean()); Assert.Equal(8m, r.GetProperty("fuel_total_litres").GetDecimal());
        Assert.Equal(6, r.GetProperty("roster").GetArrayLength()); Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"grid_available\":[1]}")]
    [InlineData("{\"generator_capacity_kw\":\"10\"}")]
    [InlineData("{\"unknown_field\":true}")]
    [InlineData("{malformed")]
    public async Task InvalidJsonAndTypesReturn400WithNamedFields(string payload)
    {
        using var response = await client.PostAsync("/api/roster", new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_input", json.RootElement.GetProperty("status").GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task ValidButImpossibleScenarioReturns200Infeasible()
    {
        var input = BackupPowerSolverTests.Worked(); input.FuelBudgetLitres = 0;
        using var response = await client.PostAsJsonAsync("/api/roster", input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("infeasible", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("roster").GetArrayLength());
    }

    [Theory]
    [InlineData("Resident", 9002)] [InlineData("DPDCOfficial", 9001)]
    public async Task PublishedPlanImportsOnlyKnownHoursAndRejectsUnpublishedDays(string role, int id)
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", role); client.DefaultRequestHeaders.Add("X-Test-Id", id.ToString());
        var today = DateOnly.FromDateTime(DhakaTime.ToLocal(DateTime.UtcNow));
        using var response = await client.GetAsync($"/api/backup-power/published-grid?zoneId=1&date={today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { true,true,false,false,false,true,true,true,true,true }, json.RootElement.GetProperty("grid_available").EnumerateArray().Select(x => x.GetBoolean()));
        Assert.Contains("not a live grid measurement", json.RootElement.GetProperty("message").GetString());
        using var unknown = await client.GetAsync($"/api/backup-power/published-grid?zoneId=1&date={today.AddDays(1):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.Conflict, unknown.StatusCode);
    }

    [Fact]
    public async Task ImportRequiresAuthorizedAccountAndDoesNotChangePublicSolver()
    {
        using var anonymous = await client.GetAsync("/api/backup-power/published-grid?zoneId=1&date=2026-11-04");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Crew"); client.DefaultRequestHeaders.Add("X-Test-Id", "9003");
        using var crew = await client.GetAsync("/api/backup-power/published-grid?zoneId=1&date=2026-11-04");
        Assert.Equal(HttpStatusCode.Forbidden, crew.StatusCode);
    }

    public async Task DisposeAsync()
    {
        client?.Dispose(); await app.StopAsync(); await app.DisposeAsync();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
