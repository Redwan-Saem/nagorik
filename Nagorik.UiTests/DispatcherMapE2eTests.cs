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
        builder.Services.AddScoped<PublicMapQueryService>();
        builder.Services.AddScoped<FlaggedDrainageQueryService>();
        builder.Services.AddScoped<CrewAssignmentService>();
        builder.Services.AddScoped<DrainageDispatchService>();
        builder.Services.AddSingleton<ICrewTaskNotifier,NullCrewTaskNotifier>();
        builder.Services.AddSingleton<INotificationService>(new UiNotifications());
        builder.Services.AddScoped<ResidentReportStatusNotifier>();
        builder.Services.AddScoped<DispatcherTaskStatusNotifier>();
        builder.Services.AddScoped<ReportStatusWorkflowService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<LoadsheddingRotationEngine>();
        builder.Services.AddScoped<LoadsheddingScheduleService>();
        builder.Services.AddScoped<ResidentScheduleQueryService>();
        builder.Services.AddScoped<TransportSearchService>();
        builder.Services.AddScoped<BusOccupancyService>();
        builder.Services.AddScoped<MunicipalScheduleService>();
        builder.Services.AddScoped<CivicCaseService>();
        builder.Services.AddScoped<GeographicDataService>();builder.Services.AddScoped<TransportCatalogService>();
        builder.Services.AddMemoryCache();builder.Services.Configure<AirQualityOptions>(_=>{});builder.Services.AddSingleton<AirQualityCache>();builder.Services.AddScoped<AirQualityService>();
        builder.Services.AddSingleton<UiAirQualityProvider>();builder.Services.AddSingleton<IAirQualityProvider>(s=>s.GetRequiredService<UiAirQualityProvider>());
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
        app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.MigrateAsync();
            db.Users.Add(new User { Email = "dispatcher@map.invalid", Name = "Map fixture", Role = "Dispatcher", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4) });
            db.Users.Add(new User { Email = "admin@map.invalid", Name = "Admin fixture", Role = "Admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4) });
            await db.SaveChangesAsync();
            await DevelopmentMapSeeder.SeedAsync(db, app.Environment, builder.Configuration);
            var owner = new User { Email = "resident@map.invalid", Name = "Fixture resident", Role = "Resident", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password,4) };
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

    private sealed class UiNotifications:INotificationService
    {
        public Task<int> SendToUserAsync(string userId,string title,string body,string target,DateTime? predictionTimestampUtc=null,CancellationToken ct=default)=>Task.FromResult(1);
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public void HomepageShowsPublicAndResidentWorkspacesAndSignsOut(int width)
    {
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument($"--window-size={width},1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        if(width<600)browser.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride",new Dictionary<string,object?>{{"width",width},{"height",844},{"deviceScaleFactor",1},{"mobile",true}});
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By by)=>wait.Until(d=>{try{var e=d.FindElement(by);if(!e.Displayed||!e.Enabled)return false;browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",e);e.Click();return true;}catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}});
        browser.Navigate().GoToUrl(url+"/");Assert.Equal(6,browser.FindElements(By.CssSelector("#dashboard-public .dashboard-card")).Count);Assert.Empty(browser.FindElements(By.Id("dashboard-work")));
        Click(By.CssSelector(".dashboard-actions a[href^='/login.html']"));browser.FindElement(By.Id("email")).SendKeys("resident@map.invalid");browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));
        wait.Until(d=>d.FindElements(By.CssSelector("#dashboard-work .dashboard-card")).Count==9);Assert.Empty(browser.FindElements(By.CssSelector("#dashboard-work a[href^='/admin/']")));Assert.Contains("Noise concerns",browser.FindElement(By.Id("dashboard-work")).Text);
        Assert.True(browser.ExecuteScript("return document.documentElement.scrollWidth <= window.innerWidth + 1") is true);
        var capture=Environment.GetEnvironmentVariable("NAGORIK_SCREENSHOT_DIR");if(!string.IsNullOrWhiteSpace(capture)){Directory.CreateDirectory(capture);browser.GetScreenshot().SaveAsFile(Path.Combine(capture,$"homepage-{width}.png"));}
        Click(By.CssSelector("#dashboard-work a[href='/resident/loadshedding']"));wait.Until(d=>d.FindElements(By.CssSelector("#schedule-days .schedule-day")).Count==7);Click(By.CssSelector("header a.brand"));wait.Until(d=>d.FindElements(By.Id("dashboard-work")).Count==1);
        if(width<760)Click(By.Id("site-menu-toggle"));
        Click(By.CssSelector("form[action='/session/logout'] button"));wait.Until(d=>d.FindElements(By.Id("dashboard-public")).Count==1&&d.FindElements(By.Id("dashboard-work")).Count==0);Assert.Null(browser.Manage().Cookies.GetCookieNamed("auth_token"));
    }

    [Fact]
    public async Task AdminManagesSourcedGeographyAndImportsReviewedCatalogInBrowser()
    {
        await using(var scope=app.Services.CreateAsyncScope())await DevelopmentTransportSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(),app.Environment,new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=1280,1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By by)=>wait.Until(d=>{try{var e=d.FindElement(by);if(!e.Displayed||!e.Enabled)return false;browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",e);e.Click();return true;}catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}});
        browser.Navigate().GoToUrl(url+"/login.html?returnUrl=%2Fadmin%2Fdata");browser.FindElement(By.Id("email")).SendKeys("admin@map.invalid");browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));wait.Until(d=>d.FindElements(By.Id("managed-zone")).Count==1);
        browser.FindElement(By.Id("Input_Code")).SendKeys("G4");browser.FindElement(By.Id("Input_Name")).SendKeys("Browser managed demo zone");browser.FindElement(By.Id("Input_Source")).SendKeys("Browser survey demonstration");browser.FindElement(By.Id("Input_BoundaryGeoJson")).SendKeys("{}");Click(By.Id("Input_IsSample"));Click(By.CssSelector("#managed-zone button"));
        wait.Until(d=>d.FindElement(By.TagName("body")).Text.Contains("Use a valid Dhaka"));Assert.Equal("G4",browser.FindElement(By.Id("Input_Code")).GetAttribute("value"));
        browser.FindElement(By.Id("Input_BoundaryGeoJson")).Clear();browser.FindElement(By.Id("Input_BoundaryGeoJson")).SendKeys("{\"type\":\"Polygon\",\"coordinates\":[[[90.44,23.74],[90.45,23.74],[90.45,23.75],[90.44,23.75],[90.44,23.74]]]}");Click(By.CssSelector("#managed-zone button"));
        wait.Until(d=>d.FindElement(By.TagName("body")).Text.Contains("Geographic data saved"));Assert.Contains("Browser survey demonstration",browser.FindElement(By.TagName("body")).Text);
        var raw=Assert.IsType<string>(browser.ExecuteAsyncScript("const done=arguments[arguments.length-1]; fetch('/api/admin/data/transport').then(r=>r.json()).then(x=>{x.source='Browser curated demonstration catalog';done(JSON.stringify(x));});"));browser.ExecuteScript("arguments[0].value=arguments[1]",browser.FindElement(By.Id("catalogJson")),raw);Click(By.CssSelector("#managed-catalog button"));
        wait.Until(d=>d.FindElement(By.TagName("body")).Text.Contains("Transport catalog imported at revision 1"));Assert.Contains("Browser curated demonstration catalog",browser.FindElement(By.TagName("body")).Text);
        browser.Navigate().GoToUrl(url+"/map?zone=4");wait.Until(d=>d.FindElement(By.Id("public-zones-status")).Text.Contains("4 zones loaded"));Click(By.CssSelector("#resident-risk-map path.leaflet-interactive"));wait.Until(d=>d.FindElement(By.Id("public-zone-details")).Text.Contains("Browser survey demonstration"));
        await using var finalScope=app.Services.CreateAsyncScope();var final=finalScope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(2,await final.ManagedDataChanges.CountAsync());Assert.Equal(1,(await final.TransportCatalogStates.SingleAsync()).Revision);
    }

    [Theory]
    [InlineData("noise")]
    [InlineData("construction")]
    public async Task CivicConcernsSubmitTrackAndReceiveRecordedOfficialReview(string kind)
    {
        await using(var scope=app.Services.CreateAsyncScope())
        {var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.Users.Add(new(){Email="reviewer@fixture.invalid",Name="Review fixture",Role="Official",PasswordHash=BCrypt.Net.BCrypt.HashPassword(Password,4)});await db.SaveChangesAsync();}
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=1280,1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By by)=>wait.Until(d=>{try{var e=d.FindElement(by);if(!e.Displayed||!e.Enabled)return false;browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",e);e.Click();return true;}catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}});
        void Login(string email,string path){browser.Manage().Cookies.DeleteCookieNamed("auth_token");browser.Navigate().GoToUrl(url+"/login.html?returnUrl="+Uri.EscapeDataString(path));browser.FindElement(By.Id("email")).SendKeys(email);browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));wait.Until(d=>!d.Url.Contains("login.html"));}
        Login("resident@map.invalid","/resident/concerns/"+kind);wait.Until(d=>d.FindElements(By.Id("civic-submit")).Count==1);
        browser.FindElement(By.Id("Input_Description")).SendKeys("Persistent disturbance observed near the fixture landmark.");browser.FindElement(By.Id("Input_Address")).SendKeys("Dhanmondi fixture landmark");browser.FindElement(By.Id("Input_Latitude")).SendKeys("23.75");browser.FindElement(By.Id("Input_Longitude")).SendKeys("90.37");Click(By.CssSelector("#civic-submit button[type=submit]"));
        wait.Until(d=>d.FindElements(By.Id("civic-state")).Count==1);Assert.Contains("Pending",browser.FindElement(By.Id("civic-state")).Text);var id=int.Parse(new Uri(browser.Url).Segments.Last());
        Login("reviewer@fixture.invalid","/official/concerns/"+kind);wait.Until(d=>d.FindElements(By.CssSelector($"a[href='/official/concerns/{kind}/{id}']")).Count==1);Click(By.CssSelector($"a[href='/official/concerns/{kind}/{id}']"));
        wait.Until(d=>d.FindElements(By.Id("civic-review")).Count==1);browser.FindElement(By.Id("Note")).SendKeys("Investigation opened; an inspector will review this concern.");Click(By.CssSelector("#civic-review button"));wait.Until(d=>d.FindElement(By.Id("civic-state")).Text.Contains("UnderReview"));
        browser.FindElement(By.Id("Note")).SendKeys("Inspection completed and official disposition recorded.");Click(By.CssSelector("#civic-review button"));wait.Until(d=>d.FindElement(By.Id("civic-state")).Text.Contains("Resolved"));
        Login("resident@map.invalid",$"/resident/concerns/{kind}/{id}");wait.Until(d=>d.FindElement(By.Id("civic-state")).Text.Contains("Resolved"));Assert.Contains("Inspection completed",browser.FindElement(By.TagName("body")).Text);
        await using var finalScope=app.Services.CreateAsyncScope();Assert.Equal(2,await finalScope.ServiceProvider.GetRequiredService<AppDbContext>().CivicCaseReviews.CountAsync());
    }

    private sealed class UiAirQualityProvider:IAirQualityProvider
    {
        public bool Fail;
        public Task<AirQualityReading> FetchAsync(double latitude,double longitude,CancellationToken ct=default)
        {if(Fail)throw new HttpRequestException("Fixture provider unavailable");return Task.FromResult(new AirQualityReading(133,60.1,63.8,DateTime.UtcNow,DateTime.UtcNow));}
    }
    [Fact]
    public void AirQualityShowsProviderEstimateUnknownAndNetworkRetryWithoutLosingZone()
    {
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=1280,1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        browser.Navigate().GoToUrl(url+"/air-quality");new SelectElement(browser.FindElement(By.Id("air-zone"))).SelectByValue("1");wait.Until(d=>d.FindElement(By.Id("air-reading")).Displayed);
        Assert.Contains("US AQI 133",browser.FindElement(By.Id("air-reading")).Text);Assert.Contains("Source:",browser.FindElement(By.Id("air-reading")).Text);Assert.Contains("Model time:",browser.FindElement(By.Id("air-reading")).Text);
        app.Services.GetRequiredService<UiAirQualityProvider>().Fail=true;new SelectElement(browser.FindElement(By.Id("air-zone"))).SelectByValue("3");wait.Until(d=>d.FindElement(By.Id("air-status")).Text.Contains("Unknown"));Assert.False(browser.FindElement(By.Id("air-reading")).Displayed);
        browser.ExecuteCdpCommand("Network.enable",new Dictionary<string,object?>());browser.ExecuteCdpCommand("Network.emulateNetworkConditions",new Dictionary<string,object?>{{"offline",true},{"latency",0},{"downloadThroughput",0},{"uploadThroughput",0}});
        new SelectElement(browser.FindElement(By.Id("air-zone"))).SelectByValue("2");wait.Until(d=>d.FindElement(By.Id("air-status")).Text.Contains("zone is kept"));Assert.Equal("2",browser.FindElement(By.Id("air-zone")).GetAttribute("value"));
        browser.ExecuteCdpCommand("Network.emulateNetworkConditions",new Dictionary<string,object?>{{"offline",false},{"latency",0},{"downloadThroughput",-1},{"uploadThroughput",-1}});app.Services.GetRequiredService<UiAirQualityProvider>().Fail=false;
        browser.FindElement(By.Id("air-refresh")).Click();wait.Until(d=>d.FindElement(By.Id("air-reading")).Displayed);Assert.Contains("Model estimate",browser.FindElement(By.Id("air-status")).Text);
    }

    [Theory]
    [InlineData("water","UtilityOfficial")]
    [InlineData("waste","Official")]
    public async Task MunicipalScheduleDraftPublishEditAndRetireWorkInBrowser(string kind,string role)
    {
        await using(var scope=app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.Users.Add(new(){Email="scheduler@fixture.invalid",Role=role,Name="Schedule official",PasswordHash=BCrypt.Net.BCrypt.HashPassword(Password,4)});await db.SaveChangesAsync();
        }
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=1280,1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By by)=>wait.Until(d=>{try{var e=d.FindElement(by);if(!e.Displayed||!e.Enabled)return false;browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",e);e.Click();return true;}catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}});
        browser.Navigate().GoToUrl(url+"/services/"+kind+"?zoneId=1");Assert.Equal(7,browser.FindElements(By.CssSelector(".service-day")).Count);Assert.Contains("Unknown",browser.FindElement(By.TagName("body")).Text);
        browser.Navigate().GoToUrl(url+"/login.html?returnUrl="+Uri.EscapeDataString("/official/services/"+kind));browser.FindElement(By.Id("email")).SendKeys("scheduler@fixture.invalid");browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));
        wait.Until(d=>d.FindElements(By.Id("service-draft")).Count==1);new SelectElement(browser.FindElement(By.Id("Input_ZoneId"))).SelectByValue("1");browser.FindElement(By.Id("Input_Source")).SendKeys("Browser district bulletin fixture");browser.FindElement(By.Id("Input_Notes")).SendKeys("Browser schedule note");Click(By.CssSelector("#service-draft button"));
        wait.Until(d=>d.FindElements(By.CssSelector(".service-record")).Count==1);Assert.Contains("Draft",browser.FindElement(By.CssSelector(".service-record")).Text);
        Click(By.CssSelector(".service-record a"));wait.Until(d=>d.Url.Contains("?id="));browser.FindElement(By.Id("Input_Notes")).Clear();browser.FindElement(By.Id("Input_Notes")).SendKeys("Updated browser schedule note");Click(By.CssSelector("#service-draft button"));
        wait.Until(d=>d.FindElement(By.CssSelector(".service-record")).Text.Contains("Updated browser schedule note"));
        browser.Navigate().GoToUrl(url+"/services/"+kind+"?zoneId=1");Assert.DoesNotContain("Updated browser",browser.FindElement(By.TagName("body")).Text);
        browser.Navigate().GoToUrl(url+"/official/services/"+kind);Click(By.CssSelector(".service-record input[name=confirm]"));Click(By.CssSelector(".service-record button"));wait.Until(d=>d.FindElement(By.CssSelector(".service-record h3")).Text.Contains("Published"));
        browser.Manage().Cookies.DeleteCookieNamed("auth_token");browser.Navigate().GoToUrl(url+"/services/"+kind+"?zoneId=1");Assert.Equal(7,browser.FindElements(By.CssSelector(".service-day")).Count);Assert.Contains("08:00–10:00",browser.FindElement(By.TagName("body")).Text);Assert.Contains("Updated browser schedule note",browser.FindElement(By.TagName("body")).Text);
        browser.Navigate().GoToUrl(url+"/login.html?returnUrl="+Uri.EscapeDataString("/official/services/"+kind));browser.FindElement(By.Id("email")).SendKeys("scheduler@fixture.invalid");browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));wait.Until(d=>d.FindElements(By.CssSelector(".service-record")).Count==1);
        Click(By.CssSelector(".service-record input[name=confirm]"));Click(By.CssSelector(".service-record button"));wait.Until(d=>d.FindElement(By.CssSelector(".service-record h3")).Text.Contains("Retired"));
        browser.Navigate().GoToUrl(url+"/services/"+kind+"?zoneId=1");Assert.Contains("Unknown",browser.FindElement(By.TagName("body")).Text);
    }

    [Fact]
    public async Task BusOccupancyIsPublicButOnlyResidentsReportAndStaleReportsExpire()
    {
        await using(var scope=app.Services.CreateAsyncScope())await DevelopmentTransportSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(),app.Environment,new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=390,1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        browser.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride",new Dictionary<string,object?>{{"width",390},{"height",844},{"deviceScaleFactor",1},{"mobile",true}});
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By by)=>wait.Until(d=>{try{var e=d.FindElement(by);if(!e.Displayed||!e.Enabled)return false;browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",e);e.Click();return true;}catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}});
        browser.Navigate().GoToUrl(url+"/transport/occupancy");wait.Until(d=>d.FindElement(By.Id("occupancy-summary")).Text.Contains("Unknown"));Assert.Empty(browser.FindElements(By.Id("occupancy-report")));
        browser.Navigate().GoToUrl(url+"/login.html?returnUrl=%2Ftransport%2Foccupancy");browser.FindElement(By.Id("email")).SendKeys("resident@map.invalid");browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));
        wait.Until(d=>d.FindElements(By.Id("occupancy-report")).Count==1&&d.FindElements(By.CssSelector("#occupancy-segment option")).Count>0);
        new SelectElement(browser.FindElement(By.Id("occupancy-level"))).SelectByValue("Full");Click(By.Id("occupancy-submit"));wait.Until(d=>d.FindElement(By.Id("occupancy-report-status")).Text.Contains("Observation saved"));
        wait.Until(d=>d.FindElement(By.Id("occupancy-summary")).Text.Contains("1 recent reporters"));Click(By.Id("occupancy-submit"));wait.Until(d=>d.FindElement(By.Id("occupancy-report-status")).Text.Contains("five minutes"));
        browser.Manage().Cookies.DeleteCookieNamed("auth_token");browser.Navigate().Refresh();wait.Until(d=>d.FindElement(By.Id("occupancy-summary")).Text.Contains("Full"));Assert.Empty(browser.FindElements(By.Id("occupancy-report")));
        await using(var scope=app.Services.CreateAsyncScope())await scope.ServiceProvider.GetRequiredService<AppDbContext>().BusOccupancyReports.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ObservedAtUtc,DateTime.UtcNow.AddMinutes(-16)));
        Click(By.Id("occupancy-refresh"));wait.Until(d=>d.FindElement(By.Id("occupancy-summary")).Text.Contains("Unknown"));Assert.DoesNotContain("resident@map.invalid",browser.FindElement(By.TagName("body")).Text);
        Assert.True(browser.ExecuteScript("return document.documentElement.scrollWidth <= window.innerWidth + 1") is true);
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public async Task TransportSearchSortDetailsGpsAndNetworkRetryWork(int width)
    {
        await using(var scope=app.Services.CreateAsyncScope())
            await DevelopmentTransportSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(),app.Environment,new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument($"--window-size={width},1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        if(width<600)browser.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride",new Dictionary<string,object?>{{"width",width},{"height",844},{"deviceScaleFactor",1},{"mobile",true}});
        browser.ExecuteCdpCommand("Browser.grantPermissions",new Dictionary<string,object?>{{"origin",url},{"permissions",new[]{"geolocation"}}});
        browser.ExecuteCdpCommand("Emulation.setGeolocationOverride",new Dictionary<string,object?>{{"latitude",23.7562},{"longitude",90.3758},{"accuracy",10}});
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By by)=>wait.Until(d=>{try{var e=d.FindElement(by);if(!e.Displayed||!e.Enabled)return false;browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",e);e.Click();return true;}catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}});
        browser.Navigate().GoToUrl(url+"/transport");wait.Until(d=>d.FindElement(By.Id("transport-status")).Text.Contains("Landmarks ready"));
        Click(By.Id("transport-search"));Assert.Contains("Choose both",browser.FindElement(By.Id("transport-validation")).Text);
        browser.FindElement(By.Id("transport-origin")).SendKeys("Dhanmondi 27");browser.FindElement(By.Id("transport-destination")).SendKeys("Motijheel");Click(By.Id("transport-search"));
        wait.Until(d=>d.FindElements(By.CssSelector(".transport-option")).Count>=2);
        Assert.Contains("Demonstration route",browser.FindElement(By.Id("transport-options")).Text);Assert.Contains("Estimated BDT",browser.FindElement(By.Id("transport-options")).Text);
        new SelectElement(browser.FindElement(By.Id("transport-sort"))).SelectByValue("cheapest");
        wait.Until(d=>d.FindElements(By.CssSelector(".transport-option h3")).FirstOrDefault()?.Text=="Bus");
        Click(By.CssSelector(".transport-option button"));wait.Until(d=>d.FindElement(By.Id("transport-detail")).Displayed);
        Assert.Contains("Fare source",browser.FindElement(By.Id("transport-legs")).Text);
        Click(By.CssSelector(".transport-option a"));wait.Until(d=>d.Url.Contains("/transport/route?")&&d.FindElement(By.Id("transport-detail")).Displayed);
        Click(By.Id("transport-gps"));wait.Until(d=>d.FindElement(By.Id("transport-origin")).GetAttribute("value")=="Current location");Click(By.Id("transport-search"));
        wait.Until(d=>d.FindElements(By.CssSelector(".transport-option")).Count>=2);
        browser.ExecuteCdpCommand("Network.enable",new Dictionary<string,object?>());browser.ExecuteCdpCommand("Network.emulateNetworkConditions",new Dictionary<string,object?>{{"offline",true},{"latency",0},{"downloadThroughput",0},{"uploadThroughput",0}});
        Click(By.Id("transport-search"));wait.Until(d=>d.FindElement(By.Id("transport-status")).Text.Contains("Your locations are kept"));Assert.Equal("Motijheel",browser.FindElement(By.Id("transport-destination")).GetAttribute("value"));
        browser.ExecuteCdpCommand("Network.emulateNetworkConditions",new Dictionary<string,object?>{{"offline",false},{"latency",0},{"downloadThroughput",-1},{"uploadThroughput",-1}});
        Click(By.Id("transport-retry"));wait.Until(d=>d.FindElements(By.CssSelector(".transport-option")).Count>=2);
        Assert.True(browser.ExecuteScript("return document.documentElement.scrollWidth <= window.innerWidth + 1") is true);
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public async Task ResidentScheduleHidesDraftsRefreshesAndReopensOffline(int width)
    {
        int actorId,planId;
        await using(var scope=app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var actor=new User{Email="schedule@resident.invalid",Name="Schedule publisher",Role="UtilityOfficial",PasswordHash="fixture"};db.Users.Add(actor);await db.SaveChangesAsync();actorId=actor.Id;
            var today=DateOnly.FromDateTime(DhakaTime.ToLocal(DateTime.UtcNow));
            var row=await scope.ServiceProvider.GetRequiredService<LoadsheddingScheduleService>().GenerateAsync(new(today,today.AddDays(6),10,120,[new(1,10),new(2,10)]),actorId);planId=row.Id;
        }
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument($"--window-size={width},1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var driverService=ChromeDriverService.CreateDefaultService();driverService.HideCommandPromptWindow=true;
        browser=new ChromeDriver(driverService,options,TimeSpan.FromSeconds(90));
        if(width<600)browser.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride",new Dictionary<string,object?>{{"width",width},{"height",844},{"deviceScaleFactor",1},{"mobile",true}});
        browser.ExecuteCdpCommand("Browser.grantPermissions",new Dictionary<string,object?>{{"origin",url},{"permissions",new[]{"geolocation"}}});
        browser.ExecuteCdpCommand("Emulation.setGeolocationOverride",new Dictionary<string,object?>{{"latitude",23.7461},{"longitude",90.3742},{"accuracy",10}});
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(45));wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By by)=>wait.Until(d=>
        {
            try{var target=d.FindElement(by);if(!target.Displayed||!target.Enabled)return false;browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",target);target.Click();return true;}
            catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}
        });
        browser.Navigate().GoToUrl(url+"/login.html?returnUrl=%2Fresident%2Floadshedding%3Fzone%3D1");
        browser.FindElement(By.Id("email")).SendKeys("resident@map.invalid");browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));
        wait.Until(d=>d.FindElements(By.CssSelector("#schedule-days .schedule-day")).Count==7);
        Assert.Contains("not yet published",browser.FindElement(By.Id("schedule-publication-summary")).Text);
        // Polling deliberately pauses in hidden tabs. Keep this real browser page
        // foregrounded while checking the automatic publication update.
        browser.ExecuteCdpCommand("Page.bringToFront", new Dictionary<string,object?>());
        Assert.False(browser.ExecuteScript("return document.hidden") is true);
        await using(var scope=app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LoadsheddingScheduleService>().PublishAsync(planId,new(){ExpectedRevision=1,Confirm=true,AcknowledgeWarnings=true},actorId);
        // Do not click Refresh: the real 30-second publication poll must pick it up.
        try { wait.Until(d=>d.FindElement(By.Id("schedule-publication-summary")).Text.Contains("Published seven-day")); }
        catch(WebDriverTimeoutException e)
        {
            throw new Xunit.Sdk.XunitException($"Automatic publication refresh failed: summary={browser.FindElement(By.Id("schedule-publication-summary")).Text}; status={browser.FindElement(By.Id("resident-schedule-status")).Text}; hidden={browser.ExecuteScript("return document.hidden")}. {e.Message}");
        }
        Assert.Single(browser.FindElements(By.CssSelector(".schedule-today")));
        Assert.DoesNotContain("Not published",browser.FindElement(By.Id("schedule-last-updated")).Text);
        Click(By.Id("schedule-locate"));wait.Until(d=>d.FindElement(By.Id("schedule-location-status")).Text.Contains("Detected area"));
        new SelectElement(browser.FindElement(By.Id("schedule-zone"))).SelectByValue("3");
        wait.Until(d=>d.FindElement(By.Id("schedule-publication-summary")).Text.Contains("not yet published"));
        new SelectElement(browser.FindElement(By.Id("schedule-zone"))).SelectByValue("1");
        wait.Until(d=>d.FindElement(By.Id("schedule-publication-summary")).Text.Contains("Published seven-day"));
        wait.Until(d=>browser.ExecuteScript("return !!navigator.serviceWorker.controller") is true);
        Assert.True(browser.ExecuteAsyncScript("const done=arguments[arguments.length-1]; caches.open('nagorik-resident-schedule-shell-v1').then(c=>c.match('/resident-loadshedding-offline.html')).then(r=>done(!!r));") is true);
        browser.ExecuteCdpCommand("Network.enable",new Dictionary<string,object?>());
        browser.ExecuteCdpCommand("Network.emulateNetworkConditions",new Dictionary<string,object?>{{"offline",true},{"latency",0},{"downloadThroughput",0},{"uploadThroughput",0}});
        Click(By.Id("schedule-refresh"));wait.Until(d=>d.FindElement(By.Id("schedule-cache-notice")).Displayed);
        browser.Navigate().Refresh();
        wait.Until(d=>d.FindElements(By.CssSelector("#schedule-days .schedule-day")).Count==7 && d.FindElement(By.Id("schedule-cache-notice")).Displayed);
        Assert.Contains("last downloaded",browser.FindElement(By.Id("schedule-cache-notice")).Text);
        browser.ExecuteCdpCommand("Network.emulateNetworkConditions",new Dictionary<string,object?>{{"offline",false},{"latency",0},{"downloadThroughput",-1},{"uploadThroughput",-1}});
        browser.Navigate().Refresh();wait.Until(d=>d.FindElement(By.Id("resident-schedule-status")).Text.Contains("checked successfully"));
        Assert.False(browser.FindElement(By.Id("schedule-cache-notice")).Displayed);
        Assert.True(browser.ExecuteScript("return document.documentElement.scrollWidth <= window.innerWidth + 1") is true);
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public async Task UtilityOfficialGeneratesReviewsEditsAndPublishesSchedule(int width)
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Email="utility@schedule.invalid", Name="Utility fixture", Role="UtilityOfficial", PasswordHash=BCrypt.Net.BCrypt.HashPassword(Password,4) });
            await db.SaveChangesAsync();
        }
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument($"--window-size={width},1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var driverService=ChromeDriverService.CreateDefaultService();driverService.HideCommandPromptWindow=true;
        browser=new ChromeDriver(driverService,options,TimeSpan.FromSeconds(90));
        if(width<600)browser.ExecuteCdpCommand("Emulation.setDeviceMetricsOverride",new Dictionary<string,object?>{{"width",width},{"height",844},{"deviceScaleFactor",1},{"mobile",true}});
        var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(30));
        void Click(By by)
        {
            var target=wait.Until(d=>d.FindElements(by).FirstOrDefault(e=>e.Displayed&&e.Enabled));
            browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",target);target.Click();
        }
        browser.Navigate().GoToUrl(url+"/login.html?returnUrl=%2Fofficial%2Floadshedding");
        browser.FindElement(By.Id("email")).SendKeys("utility@schedule.invalid");browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));
        wait.Until(d=>d.FindElements(By.Id("schedule-generate")).Count==1);
        browser.FindElement(By.Name("AvailableCapacityMw")).SendKeys("10");
        browser.ExecuteScript("document.querySelector('[name=EndDate]').value=document.querySelector('[name=StartDate]').value");
        var rows=browser.FindElements(By.CssSelector(".schedule-demand")).Take(2).ToArray();
        foreach(var row in rows)
        {
            var check=row.FindElement(By.CssSelector(".zone-included"));browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",check);check.Click();
            row.FindElement(By.CssSelector(".zone-demand")).SendKeys("10");
        }
        Click(By.CssSelector("#schedule-generate button[type=submit]"));
        wait.Until(d=>d.FindElements(By.Id("review-summary")).Any(e=>e.Text.Contains("Draft")&&e.Text.Contains("Revision 1")));
        Assert.Equal(12,browser.FindElements(By.CssSelector("#review-slots tr")).Count);
        Click(By.CssSelector("#review-slots button"));Click(By.CssSelector("#schedule-edit button[type=submit]"));
        wait.Until(d=>d.FindElement(By.Id("review-summary")).Text.Contains("Revision 2"));
        Click(By.CssSelector("#schedule-publish input[name=Confirm]"));
        Click(By.CssSelector("#schedule-publish input[name=AcknowledgeWarnings]"));Click(By.CssSelector("#schedule-publish button[type=submit]"));
        wait.Until(d=>d.FindElement(By.Id("review-status")).Text.Contains("published successfully"));
        Assert.False(browser.FindElement(By.Id("schedule-edit")).Displayed);
        browser.Navigate().Refresh();wait.Until(d=>d.FindElement(By.Id("review-summary")).Text.Contains("Published"));
        Assert.True(browser.ExecuteScript("return document.documentElement.scrollWidth <= window.innerWidth + 1") is true);
        await using(var scope=app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var schedule=await db.LoadsheddingSchedules.SingleAsync();
            Assert.Equal(ScheduleState.Published,schedule.State);Assert.Equal(2,await db.SchedulePublications.CountAsync());
            Assert.Equal(new[]{"Generate","Edit","Publish"},await db.ScheduleAudits.OrderBy(a=>a.Id).Select(a=>a.Action).ToArrayAsync());
        }
    }

    [Fact]
    public async Task NonMlCrewDispatchVerificationResidentGpsAndAreasWorkAcrossRealPages()
    {
        await using(var scope=app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var crewUser=new User{Email="crew@map.invalid",Role="Crew",PasswordHash=BCrypt.Net.BCrypt.HashPassword(Password,4)};
            var secondUser=new User{Email="crew2@map.invalid",Role="Crew",PasswordHash=BCrypt.Net.BCrypt.HashPassword(Password,4)};
            var busy=new Crew{Name="Busy fixture crew",User=crewUser};
            var free=new Crew{Name="Free fixture crew",User=secondUser,LastLatitude=23.75,LastLongitude=90.38,LastSeenAtUtc=DateTime.UtcNow};
            db.Crews.AddRange(busy,free);var report=await db.Reports.SingleAsync(r=>r.Id==1);report.Status=ReportStatus.Assigned;report.AssignedCrew=busy;
            var zone=await db.Zones.SingleAsync(z=>z.Id==1);zone.RiskLevel=ZoneRiskLevel.High;zone.LastComputedAtUtc=DateTime.UtcNow;
            var resident=await db.Users.SingleAsync(u=>u.Email=="resident@map.invalid");
            db.NotificationPreferences.Add(new(){UserId=resident.Id.ToString(),NotificationsEnabled=true});await db.SaveChangesAsync();
        }
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=1280,1000");options.PageLoadStrategy=PageLoadStrategy.Eager;
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;
        browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));var wait=new WebDriverWait(browser,TimeSpan.FromSeconds(30));
        browser.ExecuteCdpCommand("Browser.grantPermissions",new Dictionary<string,object?>{{"origin",url},{"permissions",new[]{"geolocation"}}});
        browser.ExecuteCdpCommand("Emulation.setGeolocationOverride",new Dictionary<string,object?>{{"latitude",23.7461},{"longitude",90.3742},{"accuracy",10}});
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException),typeof(NoSuchElementException));
        void Click(By selector)=>wait.Until(d=>
        {
            try{var element=d.FindElement(selector);if(!element.Displayed || !element.Enabled)return false;
                browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",element);element.Click();return true;}
            catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}
        });
        void Login(string email,string destination)
        {
            browser.Navigate().GoToUrl(url+"/login.html?returnUrl="+Uri.EscapeDataString(destination));
            browser.FindElement(By.Id("email")).SendKeys(email);browser.FindElement(By.Id("password")).SendKeys(Password);Click(By.CssSelector("button"));
            wait.Until(d=>!d.Url.Contains("login.html"));
        }
        Login("crew@map.invalid","/crew/tasks/1");Click(By.Id("crew-location-capture"));
        wait.Until(d=>d.FindElement(By.Id("crew-location-status")).Text.Contains("Location ready"));Click(By.Id("update-task-status"));
        wait.Until(d=>d.FindElement(By.Id("task-status")).Text.Contains("In Progress"));
        Login("dispatcher@map.invalid","/dispatcher/map");
        wait.Until(d=>d.FindElement(By.Id("map-crews-status")).Text.Contains("2 crew locations loaded"));
        Assert.Single(browser.FindElements(By.CssSelector(".crew-busy")));Assert.Single(browser.FindElements(By.CssSelector(".crew-free")));
        browser.Navigate().GoToUrl(url+"/dispatcher/flagged-drainage");
        new SelectElement(browser.FindElement(By.Id("crew-1"))).SelectByValue("2");Click(By.CssSelector("form[action$='/1/assign'] button"));
        wait.Until(d=>d.Url.Contains("/Dispatch/Report/"));
        Assert.Contains("Assigned",browser.FindElement(By.TagName("body")).Text);
        browser.Navigate().GoToUrl(url+"/dispatcher/flagged-drainage");new SelectElement(browser.FindElement(By.Id("crew-1"))).SelectByValue("2");
        Click(By.CssSelector("form[action$='/1/assign'] button"));wait.Until(d=>d.FindElement(By.TagName("body")).Text.Contains("active task already exists"));
        browser.Navigate().GoToUrl(url+"/dispatcher/report-verification");Click(By.CssSelector("form[action='/dispatcher/report-verification/1'] button"));
        wait.Until(d=>d.FindElement(By.TagName("body")).Text.Contains("Marker verified and published"));
        browser.Manage().Cookies.DeleteCookieNamed("auth_token");browser.Navigate().GoToUrl(url+"/map?zone=1");
        wait.Until(d=>d.FindElement(By.Id("public-reports-status")).Text.Contains("1 reports loaded"));
        var workerState=browser.ExecuteAsyncScript("const done=arguments[arguments.length-1]; navigator.serviceWorker.register('/sw.js').then(()=>navigator.serviceWorker.ready).then(r=>new Promise(resolve=>{const worker=r.active;if(worker?.state==='activated'){resolve('activated');return;}worker?.addEventListener('statechange',()=>{if(worker.state==='activated')resolve('activated');});})).then(done,e=>done('error:'+e.name));");
        Assert.Equal("activated",workerState);
        Click(By.Id("resident-locate"));wait.Until(d=>d.FindElement(By.Id("resident-location-status")).Text.Contains("Dhanmondi"));
        Assert.Contains("High",browser.FindElement(By.Id("resident-location-status")).Text);
        Login("resident@map.invalid","/resident/areas");
        wait.Until(d=>d.FindElements(By.CssSelector("#area-zone option")).Count==4);
        new SelectElement(browser.FindElement(By.Id("area-zone"))).SelectByValue("1");Click(By.Id("area-save"));
        wait.Until(d=>d.FindElement(By.Id("areas-status")).Text.Contains("Area saved"));
        browser.Navigate().GoToUrl(url+"/Notifications/Settings");wait.Until(d=>d.FindElement(By.Id("save-settings")).Enabled);
        Click(By.Id("notifications-enabled"));Click(By.Id("save-settings"));wait.Until(d=>d.FindElement(By.Id("settings-status")).Text.Contains("preference saved"));
        browser.Navigate().GoToUrl(url+"/resident/areas");wait.Until(d=>d.FindElement(By.Id("areas-global-status")).Text.Contains("OFF"));
        Assert.Single(browser.FindElements(By.CssSelector("button[data-area-toggle='1']")));
        browser.Navigate().GoToUrl(url+"/Notifications/Settings");wait.Until(d=>d.FindElement(By.Id("save-settings")).Enabled);
        Click(By.Id("notifications-enabled"));Click(By.Id("save-settings"));wait.Until(d=>d.FindElement(By.Id("settings-status")).Text.Contains("preference saved"));
        browser.Navigate().GoToUrl(url+"/resident/areas");wait.Until(d=>d.FindElement(By.Id("areas-global-status")).Text.Contains("Global notifications are enabled"));
        Click(By.CssSelector("button[data-area-toggle='1']"));
        wait.Until(d=>d.FindElement(By.Id("areas-list")).Text.Contains("alerts off"));Click(By.CssSelector("button[data-area-remove='1']"));
        wait.Until(d=>d.FindElement(By.Id("areas-status")).Text.Contains("Area removed"));
        await using var finalScope=app.Services.CreateAsyncScope();var finalDb=finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(4,await finalDb.Reports.CountAsync());Assert.True((await finalDb.Reports.SingleAsync(r=>r.Id==1)).IsPubliclyVerified);
        Assert.Equal(23.7461,(await finalDb.Crews.SingleAsync(c=>c.Id==1)).LastLatitude);Assert.Empty(await finalDb.ResidentZoneSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task AdminEditsThresholdsAndVulnerabilityThenEvaluatesInBrowser()
    {
        var options = new ChromeOptions(); options.AddArgument("--headless=new"); options.AddArgument("--window-size=1280,1000");
        var service = ChromeDriverService.CreateDefaultService(); service.HideCommandPromptWindow = true;
        browser = new ChromeDriver(service, options, TimeSpan.FromSeconds(90));
        var wait = new WebDriverWait(browser, TimeSpan.FromSeconds(25));
        void Click(By selector)=>wait.Until(d=>
        {
            try{var element=d.FindElement(selector);if(!element.Displayed || !element.Enabled)return false;
                browser.ExecuteScript("arguments[0].scrollIntoView({block:'center',behavior:'instant'});",element);element.Click();return true;}
            catch(StaleElementReferenceException){return false;}catch(ElementClickInterceptedException){return false;}
        });
        browser.Navigate().GoToUrl(url + "/login.html?returnUrl=%2Fadmin%2Frisk-thresholds");
        browser.FindElement(By.Id("email")).SendKeys("admin@map.invalid");
        browser.FindElement(By.Id("password")).SendKeys(Password);
        Click(By.CssSelector("button"));
        wait.Until(d => d.FindElements(By.Id("Thresholds_MediumThreshold")).Count == 1);
        void Set(string id, string value) { var input = browser.FindElement(By.Id(id)); input.Clear(); input.SendKeys(value); }
        Set("Thresholds_MediumThreshold", "20");
        Click(By.CssSelector("form[action='/admin/risk-thresholds'] button"));
        wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains("strictly increasing (medium"));
        Set("Thresholds_MediumThreshold", "5");
        Click(By.CssSelector("form[action='/admin/risk-thresholds'] button"));
        wait.Until(d => d.FindElement(By.TagName("body")).Text.Contains("Thresholds saved."));
        Set("elevation-1", "0"); Set("density-1", "0"); Set("history-1", "1");
        Set("source-1","Browser fixture demonstration inputs");
        Click(By.CssSelector("form[action$='/zones/1/vulnerability'] button"));
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
        if(width<760)browser.FindElement(By.Id("site-menu-toggle")).Click();
        Assert.True(browser.FindElements(By.LinkText("Dispatcher risk map")).Count > 0);
        if(width<760)browser.FindElement(By.Id("site-menu-toggle")).Click();
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
        // Cluster expansion animates the map; retry a real click until the marker
        // reaches its final position instead of bypassing browser hit testing.
        wait.Until(d =>
        {
            try { d.FindElement(By.CssSelector(".report-marker")).Click(); return true; }
            catch (ElementClickInterceptedException) { return false; }
            catch (StaleElementReferenceException) { return false; }
        });
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
