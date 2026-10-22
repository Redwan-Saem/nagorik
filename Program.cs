using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=nagorik.db"));

builder.Services.AddControllersWithViews();

// Existing services
builder.Services.AddScoped<IPhotoStorage, LocalPhotoStorage>();
builder.Services.AddScoped<UtilityPhotoUploadService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<NotificationSettingsService>();

// T-004.2 Crew assignment services
// T-004.3: real notifier replaces NullCrewTaskNotifier
builder.Services.AddScoped<ICrewTaskNotifier, CrewTaskNotifier>();
builder.Services.AddScoped<CrewAssignmentService>();

// Notification services
builder.Services.AddSingleton<IPushSender, WebPushSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// T-002.5 Alert processing
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<ReportStatusWorkflowService>();
builder.Services.AddScoped<ResidentReportStatusNotifier>();
builder.Services.AddScoped<DispatcherTaskStatusNotifier>();
builder.Services.AddScoped<SosAlertService>();
builder.Services.AddOptions<SosOptions>().Bind(builder.Configuration.GetSection("Sos"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddScoped<SosResponderDirectory>();
builder.Services.AddScoped<SosResponderNotifier>();
builder.Services.AddScoped<OfficialReportQueryService>();
builder.Services.AddScoped<UtilityRoutingService>();
builder.Services.AddScoped<RiskZoneQueryService>();
builder.Services.AddScoped<MapFeatureQueryService>();
builder.Services.AddScoped<UtilityAnomalySubmissionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RiskPredictionService>();
builder.Services.AddScoped<RiskThresholdProvider, PersistentRiskThresholdProvider>();
builder.Services.AddScoped<RiskComputationService>();
builder.Services.AddOptions<RiskJobOptions>().Bind(builder.Configuration.GetSection("RiskJob"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddHostedService<RiskBackgroundService>();
builder.Services.AddOptions<RiskScoringOptions>().Bind(builder.Configuration.GetSection("RiskScoring"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<WeatherOptions>().Bind(builder.Configuration.GetSection("Weather"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>((services, client) =>
{
    // The service applies a separate bounded timeout to each attempt.
    client.Timeout = Timeout.InfiniteTimeSpan;
    client.MaxResponseContentBufferSize = 1024 * 1024;
});
builder.Services.AddHostedService<AlertBackgroundService>();
builder.Services.AddScoped<IWeatherCollector, WeatherCollectionService>();
builder.Services.AddScoped<WeatherReadingQueryService>();
builder.Services.AddHostedService<WeatherBackgroundService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            // Read token from the auth_token cookie (set by login.html)
            OnMessageReceived = ctx =>
            {
                ctx.Token ??= ctx.Request.Cookies["auth_token"];
                return Task.CompletedTask;
            },
            // For browser (non-API) requests: redirect to login page instead of
            // returning a raw 401, which browsers show as a blank error page.
            OnChallenge = ctx =>
            {
                var accept = ctx.Request.Headers["Accept"].ToString();
                bool isBrowserRequest = accept.Contains("text/html");

                if (isBrowserRequest)
                {
                    ctx.HandleResponse(); // suppress default 401 response
                    var returnUrl = Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString);
                    ctx.Response.Redirect($"/login.html?returnUrl={returnUrl}");
                }

                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(Nagorik.Api.Authorization.DispatcherMapAccess.Configure);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await DevDataSeeder.SeedAsync(scope.ServiceProvider);
    await Nagorik.Api.Data.DevelopmentMapSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Environment, app.Configuration);
    await Nagorik.Api.Data.DevelopmentReportSeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Environment, app.Configuration);
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.WaterloggingRisks.Any())
    {
        db.WaterloggingRisks.AddRange(
            new WaterloggingRisk
            {
                ZoneId = "Z1",
                ZoneName = "Dhanmondi",
                Latitude = 23.7461,
                Longitude = 90.3742,
                RiskLevel = "High",
                LastUpdated = DateTime.UtcNow
            },
            new WaterloggingRisk
            {
                ZoneId = "Z2",
                ZoneName = "Mirpur",
                Latitude = 23.8223,
                Longitude = 90.3654,
                RiskLevel = "Medium",
                LastUpdated = DateTime.UtcNow
            },
            new WaterloggingRisk
            {
                ZoneId = "Z3",
                ZoneName = "Gulshan",
                Latitude = 23.7925,
                Longitude = 90.4078,
                RiskLevel = "Low",
                LastUpdated = DateTime.UtcNow
            }
        );

        db.SaveChanges();
    }

    if (!db.ZoneMapData.Any())
    {
        db.ZoneMapData.AddRange(
            new ZoneMapData
            {
                ZoneId = "Z1",
                RiskLevel = "High",
                DrainagePumpStatus = "Working",
                BoundaryGeoJson =
                    "[[23.744,90.372],[23.748,90.372],[23.748,90.376],[23.744,90.376]]"
            },
            new ZoneMapData
            {
                ZoneId = "Z2",
                RiskLevel = "Medium",
                DrainagePumpStatus = "Faulty",
                BoundaryGeoJson =
                    "[[23.820,90.363],[23.824,90.363],[23.824,90.368],[23.820,90.368]]"
            },
            new ZoneMapData
            {
                ZoneId = "Z3",
                RiskLevel = "Low",
                DrainagePumpStatus = "Working",
                BoundaryGeoJson =
                    "[[23.790,90.406],[23.795,90.406],[23.795,90.410],[23.790,90.410]]"
            }
        );

        db.SaveChanges();
    }
}

// HTTPS redirect must come first, before auth middleware
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var summaries = new[]
{
    "Freezing",
    "Bracing",
    "Chilly",
    "Cool",
    "Mild",
    "Warm",
    "Balmy",
    "Hot",
    "Sweltering",
    "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    return forecast;
})
.WithName("GetWeatherForecast");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();

record WeatherForecast(
    DateOnly Date,
    int TemperatureC,
    string? Summary)
{
    public int TemperatureF =>
        32 + (int)(TemperatureC / 0.5556);
}
