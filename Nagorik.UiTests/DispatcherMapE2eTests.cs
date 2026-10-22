using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Nagorik.Api;
using Nagorik.Api.Controllers;
using Nagorik.Api.Data;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
using Nagorik.Api.Authorization;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Text;

public sealed class DispatcherMapE2eTests : IAsyncLifetime
{
    private const string Key = "isolated-dispatcher-map-browser-signing-key-32-bytes";
    private const string Password = "Map-fixture-123!";
    private readonly string root = Path.Combine(Path.GetTempPath(), "nagorik-map-browser-" + Guid.NewGuid().ToString("N"));
    private readonly string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private WebApplication app = null!;
    private ChromeDriver? browser;
    private string url = "";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            { ContentRootPath = root, WebRootPath = Path.Combine(repo, "wwwroot"), EnvironmentName = "Development" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:Key"] = Key;
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(root, "map.db")};Pooling=False"));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(DispatcherMapController).Assembly);
        builder.Services.AddScoped<RiskZoneQueryService>();
        builder.Services.AddScoped<MapFeatureQueryService>();
        builder.Services.AddScoped<CrewMapQueryService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<RiskPredictionService>();
        builder.Services.AddScoped<WeatherReadingQueryService>();
        builder.Services.AddScoped<RiskThresholdProvider, PersistentRiskThresholdProvider>();
        builder.Services.AddScoped<RiskComputationService>();
        builder.Services.AddScoped<IPhotoStorage, LocalPhotoStorage>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = false, ValidateAudience = false,
                ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)) };
            o.Events = new JwtBearerEvents { OnMessageReceived = c => { c.Token ??= c.Request.Cookies["auth_token"]; return Task.CompletedTask; } };
        });
        builder.Services.AddAuthorization(DispatcherMapAccess.Configure);
        app = builder.Build(); app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
            db.Users.Add(new User { Email = "dispatcher@map.invalid", Name = "Map fixture", Role = "Dispatcher", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4) });
            db.Users.Add(new User { Email = "admin@map.invalid", Name = "Admin fixture", Role = "Admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4) });
            await db.SaveChangesAsync();
            await DevelopmentMapSeeder.SeedAsync(db, app.Environment, builder.Configuration);
            var owner = new User { Email = "resident@map.invalid", Name = "Fixture resident", Role = "Resident", PasswordHash = "not-a-login-fixture" };
            db.Users.Add(owner); await db.SaveChangesAsync();
            var storage = scope.ServiceProvider.GetRequiredService<IPhotoStorage>();
            await using var photo = new MemoryStream(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a4YkAAAAASUVORK5CYII="));
            var photoUrl = await storage.SavePrivateAsync(photo, ".png");
            for (var i = 0; i < 3; i++) db.Reports.Add(new Report { UserId = owner.Id, Title = "Map fixture report " + i,
                Location = "Dhanmondi fixture", Latitude = 23.7461, Longitude = 90.3742, ImageUrl = photoUrl });
            await db.SaveChangesAsync();
        }
        await app.StartAsync(); url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    [Fact]
    public async Task AdminEditsThresholdsAndVulnerabilityThenEvaluatesInBrowser()
    {
        var options = new ChromeOptions(); options.AddArgument("--headless=new"); options.AddArgument("--window-size=1280,1000");
        var service = ChromeDriverService.CreateDefaultService(); service.HideCommandPromptWindow = true;
        browser = new ChromeDriver(service, options, TimeSpan.FromSeconds(90));
        var wait = new WebDriverWait(browser, TimeSpan.FromSeconds(25));
        browser.Navigate().GoToUrl(url + "/login.html?returnUrl=%2Fadmin%2Frisk-thresholds");
        browser.FindElement(By.Id("email")).SendKeys("admin@map.invalid");
        browser.FindElement(By.Id("password")).SendKeys(Password);
        browser.FindElement(By.CssSelector("button")).Click();
        wait.Until(d => d.FindElements(By.Id("Thresholds_MediumThreshold")).Count == 1);
        void Set(string id, string value) { var input = browser.FindElement(By.Id(id)); input.Clear(); input.SendKeys(value); }
        Set("Thresholds_MediumThreshold", "20");
        browser.FindElement(By.CssSelector("form[action='/admin/risk-thresholds'] button")).Click();
        wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains("strictly increasing (medium"));
        Set("Thresholds_MediumThreshold", "5");
        browser.FindElement(By.CssSelector("form[action='/admin/risk-thresholds'] button")).Click();
        wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains("Thresholds saved."));
        Set("elevation-1", "0"); Set("density-1", "0"); Set("history-1", "1");
        browser.FindElement(By.CssSelector("form[action$='/zones/1/vulnerability'] button")).Click();
        wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains("Vulnerability saved."));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var hour = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerHour, DateTimeKind.Utc);
            foreach (var forecast in new[] { false, true }) db.RainfallReadings.Add(new RainfallReading
            { ZoneId = 1, TimestampUtc = hour, FetchedAtUtc = now, IsForecast = forecast,
                Rainfall1HourMm = 10, Rainfall3HourMm = 30, Rainfall6HourMm = 60 });
            await db.SaveChangesAsync();
        }
        var evaluateButton = browser.FindElement(By.CssSelector("form[action$='/recompute'] button"));
        browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", evaluateButton);
        evaluateButton.Click();
        wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains("Evaluated 3 zones."));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(ZoneRiskLevel.Severe, (await db.Zones.SingleAsync(z => z.Id == 1)).RiskLevel);
            Assert.Equal(3, await db.RiskHistories.CountAsync());
            Assert.Equal(1, (await db.RiskThresholdSettings.SingleAsync()).Revision);
        }
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public void DispatcherLogsInAndSeesGeoJsonZonesInRealBrowser(int width)
    {
        var options = new ChromeOptions(); options.AddArgument("--headless=new"); options.AddArgument($"--window-size={width},1000");
        options.PageLoadStrategy = PageLoadStrategy.Eager;
        var service = ChromeDriverService.CreateDefaultService(); service.HideCommandPromptWindow = true;
        browser = new ChromeDriver(service, options, TimeSpan.FromSeconds(90));
        if (width < 600) browser.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride", new Dictionary<string, object?>
            { ["width"] = width, ["height"] = 844, ["deviceScaleFactor"] = 1, ["mobile"] = true });
        var wait = new WebDriverWait(browser, TimeSpan.FromSeconds(25));
        browser.Navigate().GoToUrl(url + "/login.html?returnUrl=%2Fdispatcher%2Fmap");
        browser.FindElement(By.Id("email")).SendKeys("dispatcher@map.invalid");
        browser.FindElement(By.Id("password")).SendKeys(Password);
        browser.FindElement(By.CssSelector("button")).Click();
        wait.Until(d => d.FindElements(By.Id("map-status")).Count == 1 && d.FindElement(By.Id("map-status")).Text.Contains("3 risk zones loaded"));
        Assert.Equal(3, browser.FindElements(By.CssSelector("#risk-zone-map path.leaflet-interactive")).Count);
        Assert.Contains("uncalibrated rule baseline", browser.FindElement(By.TagName("body")).Text);
        Assert.True(browser.FindElements(By.LinkText("Dispatcher risk map")).Count > 0);
        wait.Until(d => d.FindElement(By.Id("map-drainage-status")).Text.Contains("6 drainage points loaded"));
        Assert.Equal(3, browser.FindElements(By.CssSelector(".drainage-blocked")).Count);
        Assert.Equal(3, browser.FindElements(By.CssSelector(".drainage-clear")).Count);
        var drainageToggle = browser.FindElement(By.Id("map-drainage-toggle"));
        browser.ExecuteScript("arguments[0].click();", drainageToggle);
        wait.Until(d => d.FindElements(By.CssSelector(".drainage-marker")).Count == 0);
        wait.Until(d => d.FindElement(By.Id("map-reports-status")).Text.Contains("3 located reports loaded"));
        var cluster = wait.Until(d => d.FindElements(By.CssSelector(".report-cluster")).FirstOrDefault(e => e.Displayed));
        browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", cluster);
        cluster.Click();
        wait.Until(d => d.FindElements(By.CssSelector(".report-marker")).Count == 3);
        var reportMarker = browser.FindElement(By.CssSelector(".report-marker"));
        reportMarker.Click();
        wait.Until(d => d.FindElements(By.CssSelector(".map-report-photo")).Any(img => browser.ExecuteScript("return arguments[0].naturalWidth > 0", img) is true));
        Assert.Contains("Status: Pending", browser.FindElement(By.CssSelector(".map-report-popup")).Text);
        var reportToggle = browser.FindElement(By.Id("map-reports-toggle"));
        browser.ExecuteScript("arguments[0].click();", reportToggle);
        wait.Until(d => d.FindElements(By.CssSelector(".report-marker, .report-cluster")).Count == 0);
        browser.ExecuteScript("arguments[0].click();", drainageToggle);
        wait.Until(d => d.FindElements(By.CssSelector(".drainage-marker")).Count == 6);
        var clearMarker = browser.FindElement(By.CssSelector(".drainage-clear"));
        var changedPoint = clearMarker.GetAttribute("title")!.Split('—')[0].Trim();
        browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", clearMarker);
        clearMarker.Click();
        wait.Until(d => d.FindElements(By.CssSelector("button[data-drainage-status='Blocked']")).Any(b => b.Displayed));
        browser.FindElement(By.CssSelector("button[data-drainage-status='Blocked']")).Click();
        wait.Until(d => d.FindElement(By.Id("map-drainage-action")).Text.Contains("marked Blocked"));
        wait.Until(d => d.FindElements(By.CssSelector(".drainage-blocked")).Count == 4);
        Assert.Equal(2, browser.FindElements(By.CssSelector(".drainage-clear")).Count);
        browser.Navigate().Refresh();
        wait.Until(d => d.FindElements(By.CssSelector(".drainage-blocked")).Count == 4);
        Assert.Equal(2, browser.FindElements(By.CssSelector(".drainage-clear")).Count);
        // Hide reports after refresh before checking a zone under the markers.
        reportToggle = browser.FindElement(By.Id("map-reports-toggle"));
        browser.ExecuteScript("arguments[0].click();", reportToggle);
        var blockedMarker = browser.FindElement(By.CssSelector($".drainage-blocked[title^='{changedPoint}']"));
        browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", blockedMarker);
        blockedMarker.Click();
        wait.Until(d => d.FindElements(By.CssSelector("button[data-drainage-status='Clear']")).Any(b => b.Displayed));
        browser.FindElement(By.CssSelector("button[data-drainage-status='Clear']")).Click();
        wait.Until(d => d.FindElement(By.Id("map-drainage-action")).Text.Contains("marked Clear"));
        wait.Until(d => d.FindElements(By.CssSelector(".drainage-clear")).Count == 3);
        drainageToggle = browser.FindElement(By.Id("map-drainage-toggle"));
        // A drainage marker sits at the sample polygon's center; hide that layer
        // while testing the underlying polygon rather than bypassing click targeting.
        browser.ExecuteScript("arguments[0].click();", drainageToggle);
        wait.Until(d => d.FindElements(By.CssSelector(".drainage-marker")).Count == 0);
        var colors = browser.FindElements(By.CssSelector("#risk-zone-map path.leaflet-interactive")).Select(p => p.GetAttribute("fill")).ToArray();
        Assert.Contains("#dc2626", colors); Assert.Contains("#d97706", colors); Assert.Contains("#15803d", colors);
        Assert.Contains("Unknown", browser.FindElement(By.Id("map-legend")).Text);
        Assert.Contains("Severe", browser.FindElement(By.Id("map-legend")).Text);
        var polygon = browser.FindElement(By.CssSelector("#risk-zone-map path.leaflet-interactive"));
        browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", polygon);
        polygon.Click();
        wait.Until(d => d.FindElements(By.CssSelector(".leaflet-popup-content")).Any(p => p.Text.Contains("Not computed yet")));
        Assert.Contains("Not computed yet", browser.FindElement(By.CssSelector(".leaflet-popup-content")).Text);
        Assert.Contains("Risk:", browser.FindElement(By.Id("map-zone-details")).Text);
        var toggle = browser.FindElement(By.Id("map-zone-toggle"));
        browser.ExecuteScript("arguments[0].click();", toggle);
        wait.Until(d => d.FindElements(By.CssSelector("#risk-zone-map path.leaflet-interactive")).Count == 0);
        browser.ExecuteScript("arguments[0].click();", toggle);
        wait.Until(d => d.FindElements(By.CssSelector("#risk-zone-map path.leaflet-interactive")).Count == 3);
        Assert.True(browser.ExecuteScript("return document.documentElement.scrollWidth <= window.innerWidth + 1") is true);
    }

    public async Task DisposeAsync()
    {
        browser?.Quit(); browser?.Dispose(); if (app != null) await app.DisposeAsync();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
