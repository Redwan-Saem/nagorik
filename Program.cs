using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
using Nagorik.Api.Models;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=nagorik.db"));
builder.Services.AddControllers();


var jwtKey = "ThisIsMySecretKeyForNagorikPleaseChangeLater123!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.WaterloggingRisks.Any())
    {
        db.WaterloggingRisks.AddRange(
            new WaterloggingRisk { ZoneId = "Z1", ZoneName = "Dhanmondi", Latitude = 23.7461, Longitude = 90.3742, RiskLevel = "High", LastUpdated = DateTime.Now },
            new WaterloggingRisk { ZoneId = "Z2", ZoneName = "Mirpur", Latitude = 23.8223, Longitude = 90.3654, RiskLevel = "Medium", LastUpdated = DateTime.Now },
            new WaterloggingRisk { ZoneId = "Z3", ZoneName = "Gulshan", Latitude = 23.7925, Longitude = 90.4078, RiskLevel = "Low", LastUpdated = DateTime.Now }
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
                BoundaryGeoJson = "[[23.744,90.372],[23.748,90.372],[23.748,90.376],[23.744,90.376]]"
            },
            new ZoneMapData
            {
                ZoneId = "Z2",
                RiskLevel = "Medium",
                DrainagePumpStatus = "Faulty",
                BoundaryGeoJson = "[[23.820,90.363],[23.824,90.363],[23.824,90.368],[23.820,90.368]]"
            },
            new ZoneMapData
            {
                ZoneId = "Z3",
                RiskLevel = "Low",
                DrainagePumpStatus = "Working",
                BoundaryGeoJson = "[[23.790,90.406],[23.795,90.406],[23.795,90.410],[23.790,90.410]]"
            }
        );
        db.SaveChanges();
    }
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
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

app.MapControllers();
app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
