using System.Text;
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
using Nagorik.Api.Models;
using Nagorik.Api.Services;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

// Real browser + real controllers/services, with a fresh migrated SQLite file per case.
public sealed class UtilityAnomalyE2eTests : IAsyncLifetime
{
    private const string Password = "Fixture-only-12345!";
    private const string Key = "isolated-utility-browser-test-key-at-least-32-bytes";
    private readonly string root = Path.Combine(Path.GetTempPath(), "nagorik-browser-" + Guid.NewGuid().ToString("N"));
    private readonly string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private WebApplication app = null!;
    private ChromeDriver? browser;
    private string url = "";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(repo, "wwwroot") });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:Key"] = Key;
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(root, "isolated.db")};Pooling=False"));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ReportsController).Assembly);
        builder.Services.AddScoped<IReportService, ReportService>(); builder.Services.AddScoped<IPhotoStorage, LocalPhotoStorage>();
        builder.Services.AddScoped<UtilityPhotoUploadService>(); builder.Services.AddScoped<UtilityRoutingService>();
        builder.Services.AddScoped<UtilityAnomalySubmissionService>(); builder.Services.AddScoped<OfficialReportQueryService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = false, ValidateAudience = false,
                ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)) };
            o.Events = new JwtBearerEvents { OnMessageReceived = c => { c.Token ??= c.Request.Cookies["auth_token"]; return Task.CompletedTask; } };
        });
        builder.Services.AddAuthorization(); app = builder.Build();
        app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
            var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
            foreach (var (id, role) in new[] { (1, "Resident"), (2, "Resident"), (3, "Official"), (4, "CityOfficial") })
                db.Users.Add(new User { Id = id, Name = "Browser fixture", Email = $"fixture{id}@nagorik.invalid", Role = role, PasswordHash = hash });
            await db.SaveChangesAsync();
        }
        await app.StartAsync(); url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    [Theory]
    [InlineData(1280, 3)]
    [InlineData(390, 4)]
    public async Task ResidentSubmitsAndOfficialDashboardShowsPersistedAnomaly(int width, int officialId)
    {
        var options = new ChromeOptions(); options.AddArgument("--headless=new"); options.AddArgument($"--window-size={width},1000");
        var service = ChromeDriverService.CreateDefaultService(); service.HideCommandPromptWindow = true;
        browser = new ChromeDriver(service, options, TimeSpan.FromSeconds(90));
        if (width < 600) browser.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride", new Dictionary<string, object?>
            { ["width"] = width, ["height"] = 844, ["deviceScaleFactor"] = 1, ["mobile"] = true });
        var wait = new WebDriverWait(browser, TimeSpan.FromSeconds(20));
        void Click(By locator)
        {
            var element = browser.FindElement(locator);
            browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});", element);
            element.Click();
        }
        void Login(int id, string destination)
        {
            browser.Manage().Cookies.DeleteAllCookies();
            browser.Navigate().GoToUrl(url + "/login.html?returnUrl=" + Uri.EscapeDataString(destination));
            browser.FindElement(By.Id("email")).SendKeys($"fixture{id}@nagorik.invalid");
            browser.FindElement(By.Id("password")).SendKeys(Password);
            Click(By.CssSelector("button"));
            wait.Until(d => new Uri(d.Url).AbsolutePath == destination);
        }
        var artifacts = Path.Combine(repo, "obj", "utility-e2e"); Directory.CreateDirectory(artifacts);
        try
        {
            Login(1, "/reports/utility-anomalies/create");
            new SelectElement(browser.FindElement(By.Id("utility-AnomalyType"))).SelectByValue("GasLeak");
            wait.Until(d => d.FindElement(By.Id("utility-safety")).Displayed);
            new SelectElement(browser.FindElement(By.Id("utility-AreaId"))).SelectByValue("1");
            browser.FindElement(By.Id("utility-AddressText")).SendKeys("Fixture street, Dhanmondi");
            browser.FindElement(By.Id("utility-Description")).SendKeys("Gas pipe leaking beside the fixture street");
            // A valid 1x1 PNG exercises actual file selection, image decoding and multipart upload.
            var photo = Path.Combine(root, "fixture.png");
            await File.WriteAllBytesAsync(photo, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a4YkAAAAASUVORK5CYII="));
            browser.FindElement(By.Id("utility-Photos")).SendKeys(photo);
            wait.Until(d => d.FindElements(By.CssSelector("#utility-previews img")).Count == 1 && d.FindElement(By.Id("utility-submit")).Enabled);
            Click(By.Id("utility-submit"));
            wait.Until(d => new Uri(d.Url).AbsolutePath.StartsWith("/Reports/Confirmation/", StringComparison.Ordinal));
            var confirmationUrl = browser.Url;
            var reference = browser.FindElement(By.Id("report-code")).Text;
            Assert.Matches("^UA-[0-9]{6,}$", reference);
            browser.Navigate().Refresh(); Assert.Equal(reference, browser.FindElement(By.Id("report-code")).Text);
            Click(By.LinkText("My Reports"));
            wait.Until(d => new Uri(d.Url).AbsolutePath == "/Reports/Mine");
            var residentRow = browser.FindElement(By.CssSelector("tr.report-row"));
            Assert.Contains(reference, residentRow.Text); Assert.Contains("Submitted", residentRow.Text);
            Login(2, "/Reports/Mine"); Assert.DoesNotContain(reference, browser.FindElement(By.TagName("body")).Text);
            browser.Navigate().GoToUrl(confirmationUrl); Assert.DoesNotContain(reference, browser.PageSource);
            Login(officialId, "/official/reports");
            wait.Until(d => d.FindElement(By.Id("reports-apply")).Enabled);
            Click(By.Id("report-category-UtilityAnomaly")); Click(By.Id("reports-apply"));
            wait.Until(d => d.Url.Contains("categories=UtilityAnomaly", StringComparison.Ordinal) &&
                d.FindElements(By.CssSelector("#reports-rows tr")).Count == 1 && d.FindElement(By.Id("reports-apply")).Enabled);
            var officialRow = browser.FindElement(By.CssSelector("#reports-rows tr"));
            Assert.Contains("Utility Anomaly", officialRow.Text); Assert.Contains("High", officialRow.Text);
            Assert.Contains("GasLeak", officialRow.Text);
            await using var check = app.Services.CreateAsyncScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
            var report = await db.Reports.SingleAsync(); Assert.Equal(reference, report.ReferenceNo);
            Assert.Equal(UtilityProvider.GasAuthority, report.Utility); Assert.Equal(1, report.UserId);
            Assert.Single(report.PhotoPaths); Assert.Single(await db.ReportStatusHistories.ToListAsync());
            Assert.Single(Directory.GetFiles(Path.Combine(root, "App_Data", "report-photos")));
            browser.GetScreenshot().SaveAsFile(Path.Combine(artifacts, $"official-dashboard-{width}.png"));
        }
        catch { browser.GetScreenshot().SaveAsFile(Path.Combine(artifacts, $"failure-{width}.png")); throw; }
    }
    public async Task DisposeAsync()
    {
        browser?.Quit(); browser?.Dispose(); if (app != null) await app.DisposeAsync();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
