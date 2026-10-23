using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
        builder.Services.AddScoped<MapFeatureQueryService>();
        builder.Services.AddScoped<PublicMapQueryService>();
        builder.Services.AddScoped<CrewMapQueryService>();
        builder.Services.AddScoped<FlaggedDrainageQueryService>();
        builder.Services.AddScoped<DrainageDispatchService>();
        builder.Services.AddScoped<CrewAssignmentService>();
        builder.Services.AddSingleton<ICrewTaskNotifier, NullCrewTaskNotifier>();
        builder.Services.AddSingleton(ResidentNotificationTestHelper.Create());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<LoadsheddingRotationEngine>();
        builder.Services.AddScoped<LoadsheddingScheduleService>();
        builder.Services.AddScoped<WeatherReadingQueryService>();
        builder.Services.AddSingleton<RiskPredictionService>();
        builder.Services.AddScoped<RiskThresholdProvider, PersistentRiskThresholdProvider>();
        builder.Services.AddScoped<RiskComputationService>();
        builder.Services.AddAuthentication("MapTest").AddScheme<AuthenticationSchemeOptions, MapTestAuth>("MapTest", _ => { });
        builder.Services.AddAuthorization(DispatcherMapAccess.Configure);
        app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
            var zone = new Zone { Code = "Z1", Name = "Sample zone", RiskLevel = ZoneRiskLevel.High,
                BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[90,23],[91,23],[91,24],[90,23]]]}" };
            db.Zones.Add(zone);
            db.DrainagePoints.Add(new DrainagePoint { Zone = zone, Latitude = 23.74, Longitude = 90.37, Status = DrainageStatus.Blocked });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    [Fact]
    public async Task LegacyRiskRoutesUseCanonicalZonesAndNeverExposeLegacySeededRisk()
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.WaterloggingRisks.Add(new WaterloggingRisk { ZoneId = "Z1", ZoneName = "Obsolete seed", RiskLevel = "Low" });
            await db.SaveChangesAsync();
        }
        var risk = await client.GetStringAsync("/api/zones/Z1/risk");
        Assert.Contains("Sample zone", risk);
        Assert.Contains("High", risk);
        Assert.DoesNotContain("Obsolete seed", risk);
        var map = await client.GetStringAsync("/api/zones/map");
        Assert.Contains("FeatureCollection", map);
        Assert.DoesNotContain("Obsolete seed", map);
        Assert.DoesNotContain("factors", map, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/zones/missing/risk")).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Official", HttpStatusCode.Forbidden)]
    [InlineData("Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("UtilityOfficial", HttpStatusCode.OK)]
    [InlineData("DPDCOfficial", HttpStatusCode.OK)]
    [InlineData("DESCOOfficial", HttpStatusCode.OK)]
    public async Task OnlyUtilityOfficialsAccessScheduleManagement(string? role, HttpStatusCode expected)
    {
        if (role != null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        foreach (var path in new[] { "/api/official/loadshedding/zones", "/api/official/loadshedding/security-token" })
            Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task ScheduleDraftApiRequiresCsrfPersistsInputsAndRejectsStaleEdit()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "UtilityOfficial");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = TestDb.AddUser(db); user.Role = "UtilityOfficial"; await db.SaveChangesAsync();
        }
        var payload = new { startDate = "2026-10-23", endDate = "2026-10-23", availableCapacityMw = 10,
            slotMinutes = 120, zones = new[] { new { zoneId = 1, demandMw = 10 } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/official/loadshedding", payload)).StatusCode);
        var token = await client.GetFromJsonAsync<JsonElement>("/api/official/loadshedding/security-token");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token.GetProperty("requestToken").GetString());
        var generated = await client.PostAsJsonAsync("/api/official/loadshedding", payload);
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        var json = await generated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", json.GetProperty("state").GetString());
        Assert.Equal(1, json.GetProperty("revision").GetInt32());
        Assert.Empty(json.GetProperty("slots").EnumerateArray());
        var edit = new { expectedRevision = 1, slotId = 0, zoneId = 1, date = "2026-10-23", startMinute = 0, endMinute = 120 };
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync("/api/official/loadshedding/1/slots", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync("/api/official/loadshedding/1/slots", edit)).StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>("/api/official/loadshedding/1");
        Assert.Equal(2, saved.GetProperty("revision").GetInt32());
        Assert.Single(saved.GetProperty("slots").EnumerateArray());
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
        foreach (var route in new[] { "/dispatcher/map", "/api/map/risk-zones", "/api/map/drainage-points", "/api/map/reports", "/api/map/weather", "/api/map/crews", "/api/map/flagged-drainage" })
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

    [Fact]
    public async Task DrainageEndpointUsesPointGeoJsonAndZoneStatusProperties()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var response = await client.GetAsync("/api/map/drainage-points");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/geo+json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("FeatureCollection", json.RootElement.GetProperty("type").GetString());
        var point = json.RootElement.GetProperty("features")[0];
        Assert.Equal("Point", point.GetProperty("geometry").GetProperty("type").GetString());
        Assert.Equal(90.37, point.GetProperty("geometry").GetProperty("coordinates")[0].GetDouble());
        Assert.Equal(23.74, point.GetProperty("geometry").GetProperty("coordinates")[1].GetDouble());
        Assert.Equal("Blocked", point.GetProperty("properties").GetProperty("status").GetString());
        Assert.Equal("Z1", point.GetProperty("properties").GetProperty("zoneCode").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("omittedCount").GetInt32());
    }

    [Fact]
    public async Task ReportMapEndpointReturnsEmptyGeoJsonWithNoReports()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var response = await client.GetAsync("/api/map/reports");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/geo+json", response.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("FeatureCollection", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("features").GetArrayLength());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    private async Task<string> SecurityTokenAsync()
    {
        var html = await client.GetStringAsync("/dispatcher/map");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        return WebUtility.HtmlDecode(token.Groups[1].Value);
    }

    [Fact]
    public async Task GlobalRiskPreferenceUsesCsrfAndPreservesAllAreaMemberships()
    {
        using(var scope=app.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.Users.Add(new(){Id=1,Role="Resident"});
            db.ResidentZoneSubscriptions.Add(new(){ResidentId=1,ZoneId=1});db.AreaSubscriptions.Add(new(){UserId="1",ZoneId="Z1"});await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-Test-Role","Resident");
        using var missing=await client.PutAsJsonAsync("/api/resident/areas/global-preference",new{enabled=true});Assert.Equal(HttpStatusCode.BadRequest,missing.StatusCode);
        using var security=JsonDocument.Parse(await client.GetStringAsync("/api/resident/areas/security-token"));
        using var request=new HttpRequestMessage(HttpMethod.Put,"/api/resident/areas/global-preference"){Content=JsonContent.Create(new{enabled=true})};
        request.Headers.Add("RequestVerificationToken",security.RootElement.GetProperty("requestToken").GetString());
        using var response=await client.SendAsync(request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var finalScope=app.Services.CreateScope();var saved=finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await saved.NotificationPreferences.SingleAsync()).NotificationsEnabled);
        Assert.Single(await saved.ResidentZoneSubscriptions.ToListAsync());Assert.Single(await saved.AreaSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task AlertDeliveryDiagnosticsAreAdminOnly()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role","Resident");
        using var denied=await client.GetAsync("/admin/risk-alert-deliveries");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role");client.DefaultRequestHeaders.Add("X-Test-Role","Admin");
        var html=await client.GetStringAsync("/admin/risk-alert-deliveries");Assert.Contains("No risk alert receipts yet",html);
    }

    [Fact]
    public async Task MyAreasPageRequiresResidentAndProvidesSecurityToken()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role","Dispatcher");
        using var denied=await client.GetAsync("/resident/areas");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role");client.DefaultRequestHeaders.Add("X-Test-Role","Resident");
        var html=await client.GetStringAsync("/resident/areas");Assert.Contains("__RequestVerificationToken",html);
        Assert.Contains("/js/my-areas.js",html);Assert.Contains("My areas",html);
    }

    [Fact]
    public async Task ResidentAreasArePrivateIdempotentValidatedAndCsrfProtected()
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(new User { Id = 1, Role = "Resident" }, new User { Id = 2, Role = "Resident" });
            db.ResidentZoneSubscriptions.Add(new() { ResidentId = 2, ZoneId = 1 });
            db.AreaSubscriptions.Add(new() { UserId = "1", ZoneId = "Z1" }); await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        using (var json = JsonDocument.Parse(await client.GetStringAsync("/api/resident/areas"))) Assert.Equal(0, json.RootElement.GetProperty("subscriptions").GetArrayLength());
        using var noToken = await client.PutAsJsonAsync("/api/resident/areas/1", new { alertsEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var security = JsonDocument.Parse(await client.GetStringAsync("/api/resident/areas/security-token"));
        var token = security.RootElement.GetProperty("requestToken").GetString();
        async Task<HttpResponseMessage> Write(HttpMethod method, int id, object? payload = null)
        {
            using var request = new HttpRequestMessage(method,$"/api/resident/areas/{id}");
            request.Headers.Add("RequestVerificationToken", token);
            if (payload != null) request.Content = JsonContent.Create(payload);
            return await client.SendAsync(request);
        }
        using var subscribed = await Write(HttpMethod.Put,1,new { alertsEnabled = true, residentId = 2 }); Assert.Equal(HttpStatusCode.OK,subscribed.StatusCode);
        using var repeated = await Write(HttpMethod.Put,1,new { alertsEnabled = true }); Assert.Equal(HttpStatusCode.OK,repeated.StatusCode);
        using var invalid = await Write(HttpMethod.Put,1,new { }); Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        using var unknown = await Write(HttpMethod.Put,999,new { alertsEnabled = true }); Assert.Equal(HttpStatusCode.NotFound,unknown.StatusCode);
        using var disabled = await Write(HttpMethod.Put,1,new { alertsEnabled = false }); Assert.Equal(HttpStatusCode.OK,disabled.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(2,await db.ResidentZoneSubscriptions.CountAsync());
            Assert.False((await db.ResidentZoneSubscriptions.SingleAsync(s=>s.ResidentId==1)).AlertsEnabled);
        }
        using var removed = await Write(HttpMethod.Delete,1); Assert.Equal(HttpStatusCode.NoContent,removed.StatusCode);
        using var again = await Write(HttpMethod.Delete,1); Assert.Equal(HttpStatusCode.NoContent,again.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(2,(await db.ResidentZoneSubscriptions.SingleAsync()).ResidentId);
            Assert.Empty(await db.AreaSubscriptions.ToListAsync());
        }
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role","Dispatcher");
        using var forbidden = await client.GetAsync("/api/resident/areas"); Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
    }

    [Fact]
    public async Task VerificationRequiresDispatcherCsrfAndRevisionAndControlsPublicVisibility()
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Users.Add(new() { Id = 1, Role = "Dispatcher" });
            db.Reports.Add(new() { UserId = 1, Latitude = 23.7, Longitude = 90.3 }); await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        using var denied = await client.GetAsync("/dispatcher/report-verification"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var noCsrf = await client.PostAsync("/dispatcher/report-verification/1", new FormUrlEncodedContent(new Dictionary<string,string> { ["Publish"] = "true", ["ExpectedRevision"] = "0" }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var token = await SecurityTokenAsync();
        async Task<HttpResponseMessage> Review(bool publish, int revision) => await client.PostAsync("/dispatcher/report-verification/1", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Publish"] = publish.ToString(), ["ExpectedRevision"] = revision.ToString(), ["__RequestVerificationToken"] = token }));
        using var published = await Review(true,0); Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        using (var json = JsonDocument.Parse(await client.GetStringAsync("/api/public/map/reports"))) Assert.Equal(1, json.RootElement.GetProperty("features").GetArrayLength());
        using var stale = await Review(false,0); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var hidden = await Review(false,1); Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        using (var json = JsonDocument.Parse(await client.GetStringAsync("/api/public/map/reports"))) Assert.Equal(0, json.RootElement.GetProperty("features").GetArrayLength());
        using var finalScope = app.Services.CreateScope(); var report = await finalScope.ServiceProvider.GetRequiredService<AppDbContext>().Reports.SingleAsync();
        Assert.Equal(2, report.PublicVerificationRevision); Assert.Equal(1, report.PublicVerifiedByUserId); Assert.NotNull(report.PublicVerifiedAtUtc);
    }

    [Fact]
    public async Task ResidentMapPageIsPublicAndLoadsOnlyPublicMapScripts()
    {
        var html = await client.GetStringAsync("/map");
        Assert.Contains("resident-risk-map", html); Assert.Contains("/js/resident-map.js", html);
        Assert.DoesNotContain("/js/dispatcher-map.js", html);
        Assert.Contains("uncalibrated rule baseline", html);
    }

    [Fact]
    public async Task PublicMapIsAnonymousAndDoesNotExposeUnverifiedOrPrivateFields()
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = new User { Name = "Secret owner", Email = "secret@map.invalid" }; db.Users.Add(owner); await db.SaveChangesAsync();
            db.Reports.AddRange(new Report { UserId = owner.Id, Title = "Unverified secret", Latitude = 23.7, Longitude = 90.3 },
                new Report { UserId = owner.Id, Title = "Private title", Description = "Private description", Location = "Private address",
                    Latitude = 23.8, Longitude = 90.4, IsPubliclyVerified = true, PublicVerifiedByUserId = owner.Id, PublicVerifiedAtUtc = DateTime.UtcNow });
            db.DrainagePoints.Add(new() { ZoneId = 1, Latitude = 23.8, Longitude = 90.4, Status = DrainageStatus.Clear });
            await db.SaveChangesAsync();
        }
        foreach (var path in new[] { "/api/public/map/risk-zones", "/api/public/map/reports", "/api/public/map/blockages" })
        {
            using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/geo+json", response.Content.Headers.ContentType!.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Private", body); Assert.DoesNotContain("secret@", body); Assert.DoesNotContain("Unverified secret", body);
            Assert.DoesNotContain("crew", body, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("photoUrl", body);
            if (!path.EndsWith("risk-zones")) { using var json = JsonDocument.Parse(body); Assert.Equal(1, json.RootElement.GetProperty("features").GetArrayLength()); }
        }
    }

    [Fact]
    public async Task FlaggedPanelRequiresDispatcherAndAssignmentNeedsCsrf()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        using var forbidden = await client.GetAsync("/dispatcher/flagged-drainage"); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var page = await client.GetAsync("/dispatcher/flagged-drainage"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var noToken = await client.PostAsync("/dispatcher/flagged-drainage/1/assign", new FormUrlEncodedContent(new Dictionary<string,string> { ["CrewId"] = "1" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
    }

    [Fact]
    public async Task FlagsOnlyFreshElevatedZones()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var zone = await db.Zones.SingleAsync();
            zone.LastComputedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync();
        }
        using var fresh = JsonDocument.Parse(await client.GetStringAsync("/api/map/flagged-drainage"));
        Assert.Equal(1, fresh.RootElement.GetProperty("points").GetArrayLength());
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var zone = await db.Zones.SingleAsync();
            zone.LastComputedAtUtc = DateTime.UtcNow.AddHours(-1); await db.SaveChangesAsync();
        }
        using var stale = JsonDocument.Parse(await client.GetStringAsync("/api/map/flagged-drainage"));
        Assert.Equal(0, stale.RootElement.GetProperty("points").GetArrayLength());
    }

    [Fact]
    public async Task CrewGeoJsonOmitsUnknownLocationsAndReportsBusyAndStaleWithoutUserIdentity()
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var located = new Crew { Name = "Located", User = new User { Email = "private@crew.invalid" },
                LastLatitude = 23.7, LastLongitude = 90.3, LastSeenAtUtc = DateTime.UtcNow.AddHours(-1) };
            db.Crews.AddRange(located, new Crew { Name = "Unknown", User = new User { Email = "unknown@crew.invalid" } });
            db.Reports.Add(new Report { User = new User { Email = "owner@crew.invalid" }, AssignedCrew = located, Status = ReportStatus.InProgress });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        var body = await client.GetStringAsync("/api/map/crews"); using var json = JsonDocument.Parse(body);
        Assert.Equal(1, json.RootElement.GetProperty("omittedCount").GetInt32());
        var feature = json.RootElement.GetProperty("features")[0];
        Assert.Equal(90.3, feature.GetProperty("geometry").GetProperty("coordinates")[0].GetDouble());
        Assert.True(feature.GetProperty("properties").GetProperty("isBusy").GetBoolean());
        Assert.True(feature.GetProperty("properties").GetProperty("isStale").GetBoolean());
        Assert.DoesNotContain("private@", body); Assert.DoesNotContain("userId", body);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task OnlyAdminCanAccessThresholdScreen(string? role, HttpStatusCode expected)
    {
        if (role != null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var response = await client.GetAsync("/admin/risk-thresholds");
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AdminThresholdsRequireCsrfValidatePersistRejectStaleAndDriveComputation()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var html = await client.GetStringAsync("/admin/risk-thresholds");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpResponseMessage> Save(string medium, string high, string severe, string revision, bool csrf = true)
        {
            var values = new Dictionary<string, string> { ["Thresholds.MediumThreshold"] = medium,
                ["Thresholds.HighThreshold"] = high, ["Thresholds.SevereThreshold"] = severe, ["Revision"] = revision };
            if (csrf) values["__RequestVerificationToken"] = token;
            return await client.PostAsync("/admin/risk-thresholds", new FormUrlEncodedContent(values));
        }
        using var missing = await Save("5", "15", "30", "0", false); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var invalid = await Save("20", "15", "30", "0"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var valid = await Save("2", "4", "8", "0"); Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        using var stale = await Save("3", "5", "10", "0"); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.RiskThresholdSettings.SingleAsync(); Assert.Equal(1, saved.Revision); Assert.Equal(2, saved.MediumThreshold);
            var effective = await scope.ServiceProvider.GetRequiredService<RiskThresholdProvider>().GetAsync(); Assert.Equal(8, effective.SevereThreshold);
        }
        using var badVulnerability = await client.PostAsync("/admin/risk-thresholds/zones/1/vulnerability", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["__RequestVerificationToken"] = token, ["ElevationMeters"] = "5", ["DrainageDensityKmPerSqKm"] = "2", ["HistoryScore"] = "2" }));
        Assert.Equal(HttpStatusCode.BadRequest, badVulnerability.StatusCode);
        using var goodVulnerability = await client.PostAsync("/admin/risk-thresholds/zones/1/vulnerability", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["__RequestVerificationToken"] = token, ["ElevationMeters"] = "5", ["DrainageDensityKmPerSqKm"] = "2", ["HistoryScore"] = "0.3" }));
        Assert.Equal(HttpStatusCode.OK, goodVulnerability.StatusCode);
        using var recompute = await client.PostAsync("/admin/risk-thresholds/recompute", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, recompute.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var zone = await db.Zones.SingleAsync(); Assert.Equal(ZoneRiskLevel.Unknown, zone.RiskLevel); // no rainfall fixture
            Assert.Equal(.3, zone.HistoryScore); Assert.Single(await db.RiskHistories.ToListAsync());
            Assert.Contains("Fresh, aligned", zone.RiskFactorsJson);
        }
    }

    [Theory]
    [InlineData("Resident")]
    [InlineData("Dispatcher")]
    public async Task NonAdminCannotMutateThresholdsOrVulnerabilityOrEvaluate(string role)
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        foreach (var path in new[] { "/admin/risk-thresholds", "/admin/risk-thresholds/recompute", "/admin/risk-thresholds/zones/1/vulnerability" })
        {
            using var result = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string,string>()));
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }
    }

    [Fact]
    public async Task WeatherEndpointExplicitlyReportsMissingDataInsteadOfZeroRain()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var response = await client.GetAsync("/api/map/weather"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var zone = json.RootElement.GetProperty("zones")[0]; Assert.Equal("Unavailable", zone.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, zone.GetProperty("past").ValueKind); Assert.Equal(JsonValueKind.Null, zone.GetProperty("forecast").ValueKind);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    private async Task<HttpResponseMessage> ChangeAsync(int id, string status, string expected, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/map/drainage-points/{id}/status")
            { Content = JsonContent.Create(new { status, expectedStatus = expected }) };
        if (token != null) request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task DrainageStatusWritesPersistAndRejectStaleExpectedState()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        var token = await SecurityTokenAsync();
        using var first = await ChangeAsync(1, "Clear", "Blocked", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var stale = await ChangeAsync(1, "Blocked", "Blocked", token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/map/drainage-points"));
        Assert.Equal("Clear", json.RootElement.GetProperty("features")[0].GetProperty("properties").GetProperty("status").GetString());
        using var restored = await ChangeAsync(1, "Blocked", "Clear", token);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task StatusWriteRequiresValidAntiforgeryToken()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var missing = await ChangeAsync(1, "Clear", "Blocked", null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var forged = await ChangeAsync(1, "Clear", "Blocked", "forged");
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/map/drainage-points"));
        Assert.Equal("Blocked", json.RootElement.GetProperty("features")[0].GetProperty("properties").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("Unknown", "Blocked")]
    [InlineData("Broken", "Blocked")]
    [InlineData("", "Blocked")]
    [InlineData("Clear", "Invalid")]
    [InlineData("Clear", "")]
    public async Task InvalidStatusPayloadsAreRejected(string status, string expected)
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        using var result = await ChangeAsync(1, status, expected, await SecurityTokenAsync());
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("Official", HttpStatusCode.Forbidden)]
    [InlineData("CityOfficial", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.Forbidden)]
    public async Task OtherRolesCannotWriteDrainageStatus(string? role, HttpStatusCode expected)
    {
        if (role != null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        using var result = await ChangeAsync(1, "Clear", "Blocked", null);
        Assert.Equal(expected, result.StatusCode);
    }

    [Fact]
    public async Task MissingPointReturnsNotFoundAndZeroIdIsInvalid()
    {
        client.DefaultRequestHeaders.Add("X-Test-Role", "Dispatcher");
        var token = await SecurityTokenAsync();
        using var missing = await ChangeAsync(999, "Clear", "Unknown", token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var invalid = await ChangeAsync(0, "Clear", "Unknown", token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
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
