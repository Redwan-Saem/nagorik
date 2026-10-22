using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nagorik.Api;
using Nagorik.Api.Controllers;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
using Nagorik.Api.Authorization;

namespace Nagorik.Tests;

public class MapApiTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private WebApplication app = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(MapApiController).Assembly);
        builder.Services.AddScoped<RiskZoneQueryService>();
        builder.Services.AddAuthentication("MapTest").AddScheme<AuthenticationSchemeOptions, MapTestAuth>("MapTest", _ => { });
        builder.Services.AddAuthorization(DispatcherMapAccess.Configure);
        app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
            db.Zones.Add(new Zone { Code = "Z1", Name = "Sample zone", RiskLevel = ZoneRiskLevel.High,
                BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    [Fact]
    public async Task DispatcherGetsGeoJsonContentTypePropertiesAndCoordinates()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var response = await client.GetAsync("/api/map/risk-zones");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/geo+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("FeatureCollection", json.RootElement.GetProperty("type").GetString());
        var feature = json.RootElement.GetProperty("features")[0];
        Assert.Equal("High", feature.GetProperty("properties").GetProperty("riskLevel").GetString());
        Assert.Equal(90, feature.GetProperty("geometry").GetProperty("coordinates")[0][0][0].GetDouble());
        Assert.False(feature.GetProperty("properties").TryGetProperty("users", out _));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("Official", HttpStatusCode.Forbidden)]
    [InlineData("CityOfficial", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.Forbidden)]
    [InlineData("Dispatcher", HttpStatusCode.OK)]
    public async Task BothPageAndDataRequireDispatcherAndDoNotLeakMapToOtherRoles(string? role, HttpStatusCode expected)
    {
        if (role != null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        foreach (var route in new[] { "/dispatcher/map", "/api/map/risk-zones" })
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(expected, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            if (expected != HttpStatusCode.OK)
            {
                Assert.DoesNotContain("FeatureCollection", content);
                Assert.DoesNotContain("id=\"risk-zone-map\"", content);
                Assert.DoesNotContain("Sample zone", content);
            }
            else if (route == "/dispatcher/map")
            {
                Assert.Contains("Dispatcher risk map", content);
                Assert.Contains("id=\"map-zone-toggle\"", content);
                Assert.Contains("/dispatcher/map", content);
            }
        }
    }

    public async Task DisposeAsync()
    {
        client?.Dispose(); if (app != null) await app.DisposeAsync(); if (connection != null) await connection.DisposeAsync();
    }

    public sealed class MapTestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role) }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
