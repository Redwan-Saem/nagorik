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
            await db.SaveChangesAsync();
            await DevelopmentMapSeeder.SeedAsync(db, app.Environment, builder.Configuration);
        }
        await app.StartAsync(); url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
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
        Assert.Contains("sample risk levels", browser.FindElement(By.TagName("body")).Text);
        Assert.True(browser.FindElements(By.LinkText("Dispatcher risk map")).Count > 0);
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
