using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
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

public sealed class UtilityAnomalySubmissionApiTests : IAsyncLifetime
{
    private const string Key = "utility-submission-test-key-longer-than-thirty-two-bytes";
    private readonly string root = Path.Combine(Path.GetTempPath(), "nagorik-submission-tests-" + Guid.NewGuid().ToString("N"));
    private WebApplication app = null!;
    private HttpClient client = null!;
    private readonly SubmissionTestClock clock = new();
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(root, "test.db")};Pooling=False;Default Timeout=30"));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(UtilityAnomaliesController).Assembly);
        builder.Services.AddScoped<IPhotoStorage, LocalPhotoStorage>();
        builder.Services.AddScoped<UtilityPhotoUploadService>(); builder.Services.AddScoped<UtilityRoutingService>();
        builder.Services.AddScoped<UtilityAnomalySubmissionService>(); builder.Services.AddScoped<OfficialReportQueryService>();
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddScoped<IReportService, ReportService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
        { ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)) });
        builder.Services.AddAuthorization(); app = builder.Build();
        app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync();
            TestDb.AddUser(db, 1); TestDb.AddUser(db, 2);
            var staff = TestDb.AddUser(db, 3); staff.Role = "Official"; await db.SaveChangesAsync();
        }
        await app.StartAsync();
        client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }
    private static MultipartFormDataContent Form(string type = "GasLeak", string? description = null, string? area = "1", string? address = "Dhaka street",
        int count = 1, byte[]? bytes = null, string? latitude = null, string? longitude = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(type), "AnomalyType");
        form.Add(new StringContent(description ?? "Gas pipe leaking near the road"), "Description");
        if (area != null) form.Add(new StringContent(area), "AreaId");
        if (address != null) form.Add(new StringContent(address), "AddressText");
        if (latitude != null) form.Add(new StringContent(latitude), "Latitude");
        if (longitude != null) form.Add(new StringContent(longitude), "Longitude");
        for (var i = 0; i < count; i++)
        {
            var image = new ByteArrayContent(bytes ?? [255, 216, 255, 224, 0, 0, 0, 0]); image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(image, "Photos", $"photo{i}.jpg");
        }
        return form;
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, HttpContent? content = null, string? role = "Resident", string id = "1")
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (role != null)
        {
            var token = new JwtSecurityToken(claims: [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role)], expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return await client.SendAsync(request);
    }
    private string[] Photos() => Directory.Exists(Path.Combine(root, "App_Data")) ? Directory.GetFiles(Path.Combine(root, "App_Data"), "*", SearchOption.AllDirectories) : [];
    private async Task AssertNoWrites()
    {
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Reports.ToListAsync()); Assert.Empty(await db.ReportStatusHistories.ToListAsync()); Assert.Empty(Photos());
    }

    [Theory]
    [InlineData("ExposedWire", "High", "Unassigned")]
    [InlineData("GasLeak", "High", "GasAuthority")]
    [InlineData("FallenPole", "High", "Unassigned")]
    [InlineData("PowerOutage", "Normal", "Unassigned")]
    [InlineData("WaterLeak", "Normal", "WaterAuthority")]
    [InlineData("Other", "Normal", "Unassigned")]
    public async Task CreatesRoutedReportHistoryAndPrivatePhotos(string type, string urgency, string utility)
    {
        using var form = Form(type: type, count: 3, latitude: "23.8", longitude: "90.4");
        form.Add(new StringContent("2"), "UserId"); form.Add(new StringContent("Low"), "Urgency");
        form.Add(new StringContent("RoadDamage"), "Category"); form.Add(new StringContent("Desco"), "Utility");
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<UtilityAnomalyCreated>())!;
        Assert.Equal($"UA-{created.Id:D6}", created.ReferenceNo); Assert.Equal("UtilityAnomaly", created.Category);
        Assert.Equal(urgency, created.Urgency); Assert.Equal(utility, created.Utility); Assert.Equal("Pending", created.Status);
        Assert.Equal($"/api/reports/{created.Id}", response.Headers.Location!.ToString()); Assert.Equal(3, created.PhotoPaths.Count);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = await db.Reports.SingleAsync(); Assert.Equal(1, report.UserId); Assert.Equal(23.8, report.Latitude);
        Assert.Equal(created.PhotoPaths, report.PhotoPaths); Assert.Equal(report.PhotoPaths[0], report.ImageUrl);
        var history = await db.ReportStatusHistories.SingleAsync(); Assert.Equal(1, history.ChangedByUserId); Assert.Equal(ReportStatus.Pending, history.Status);
        using var ownPhoto = await Send(HttpMethod.Get, created.PhotoPaths[0]); Assert.Equal(HttpStatusCode.OK, ownPhoto.StatusCode);
        using var otherPhoto = await Send(HttpMethod.Get, created.PhotoPaths[0], id: "2"); Assert.Equal(HttpStatusCode.NotFound, otherPhoto.StatusCode);
        using var list = await Send(HttpMethod.Get, "/api/reports?categories=UtilityAnomaly", role: "Official", id: "3");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode); var page = (await list.Content.ReadFromJsonAsync<OfficialReportPage>())!;
        Assert.Equal(created.Id, Assert.Single(page.Items).Id);
    }
    [Theory]
    [InlineData(null, "1", 401)] [InlineData("Crew", "1", 403)] [InlineData("Official", "3", 403)]
    [InlineData("Dispatcher", "1", 403)] [InlineData("Resident", "999", 401)]
    [InlineData("Resident", "invalid", 401)] [InlineData("Resident", "3", 401)]
    public async Task EnforcesResidentIdentityBeforeWrites(string? role, string id, int status)
    {
        using var form = Form(); using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", form, role, id);
        Assert.Equal(status, (int)response.StatusCode); await AssertNoWrites();
    }
    [Theory]
    [InlineData("123456789")] [InlineData("          ")] [InlineData("    123456789    ")]
    public async Task RejectsShortDescriptions(string description)
    {
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(description: description));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await AssertNoWrites();
    }
    [Theory]
    [InlineData(10, 201)] [InlineData(1000, 201)] [InlineData(1001, 400)]
    public async Task DescriptionBoundaries(int length, int expected)
    {
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(description: new string('x', length)));
        Assert.Equal(expected, (int)response.StatusCode); if (expected == 400) await AssertNoWrites();
    }
    [Theory]
    [InlineData("999", "1", "street")] [InlineData("Unknown", "1", "street")]
    [InlineData("", "1", "street")] [InlineData("GasLeak", null, "street")]
    [InlineData("GasLeak", "0", "street")] [InlineData("GasLeak", "999", "street")]
    [InlineData("GasLeak", "1", null)] [InlineData("GasLeak", "1", "   ")]
    public async Task RejectsInvalidRequiredFields(string type, string? area, string? address)
    {
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(type: type, area: area, address: address));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await AssertNoWrites();
    }
    [Theory]
    [InlineData("23.8", null)] [InlineData(null, "90.4")] [InlineData("NaN", "90.4")]
    [InlineData("91", "90")] [InlineData("23", "181")] [InlineData("Infinity", "90")]
    public async Task RejectsInvalidCoordinates(string? latitude, string? longitude)
    {
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(latitude: latitude, longitude: longitude));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await AssertNoWrites();
    }
    [Theory]
    [InlineData(0)] [InlineData(4)]
    public async Task RejectsPhotoCount(int count)
    {
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(count: count));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await AssertNoWrites();
    }
    [Fact]
    public async Task RejectsDisguisedExecutable()
    {
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(bytes: [77, 90, 0, 0]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await AssertNoWrites();
    }
    [Fact]
    public async Task GenericEndpointCannotBypassUtilityValidation()
    {
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string> { ["Category"] = "UtilityAnomaly", ["Description"] = "Generic endpoint bypass attempt",
            ["Latitude"] = "23.8", ["Longitude"] = "90.4", ["AddressText"] = "Dhaka", ["SubmissionToken"] = "test-utility-bypass" })
            form.Add(new StringContent(field.Value), field.Key);
        var photo = new ByteArrayContent([255, 216, 255, 224, 0, 0, 0, 0]); photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(photo, "Photo", "photo.jpg");
        using var response = await Send(HttpMethod.Post, "/api/reports", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await AssertNoWrites();
    }
    [Fact]
    public async Task ConcurrentSubmissionsHaveUniqueReferences()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<UtilityAnomalyCreated>())!;
        }));
        Assert.Equal(8, results.Select(r => r.ReferenceNo).Distinct().Count());
        Assert.Equal(8, Photos().Length);
    }
    [Fact]
    public async Task ReferenceSaveFailureRollsBackReportHistoryAndPhotos()
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_utility_reference BEFORE UPDATE OF ReferenceNo ON Reports BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        }
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(count: 3));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); await AssertNoWrites();
    }
    private async Task SeedReports(int count, DateTime createdAt, int userId = 1, AnomalyType type = AnomalyType.GasLeak,
        int area = 1, string location = "Dhaka street", ReportCategory category = ReportCategory.UtilityAnomaly)
    {
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var i = 0; i < count; i++)
            db.Reports.Add(new Report { UserId = userId, Title = "Prior report", Description = "Prior anomaly report", Category = category,
                AnomalyType = type, Utility = UtilityProvider.Unassigned, Urgency = UtilityAnomalyRules.GetUrgency(type),
                CreatedAt = createdAt, AreaId = area, Location = location });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task FirstTenAcceptedThenEleventhReturnsRetryGuidanceWithoutOrphans()
    {
        for (var i = 0; i < 10; i++)
        {
            using var accepted = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        }
        using var rejected = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("3600", rejected.Headers.GetValues("Retry-After").Single());
        Assert.Equal(10, Photos().Length);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(10, await db.Reports.CountAsync()); Assert.Equal(10, await db.ReportStatusHistories.CountAsync());
    }
    [Fact]
    public async Task RollingHourExpiresExactlyAtBoundary()
    {
        await SeedReports(10, clock.GetUtcNow().UtcDateTime.AddHours(-1).AddSeconds(1));
        using var blocked = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal("1", blocked.Headers.GetValues("Retry-After").Single()); Assert.Empty(Photos());
        clock.Advance(TimeSpan.FromSeconds(1));
        using var accepted = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode); Assert.Single(Photos());
    }
    [Theory]
    [InlineData(1, "GasLeak", 1, "  DHAKA\t street  ", -23, true)]
    [InlineData(1, "GasLeak", 1, "Ｄｈａｋａ street", -23, true)]
    [InlineData(2, "GasLeak", 1, "Dhaka street", -23, false)]
    [InlineData(1, "WaterLeak", 1, "Dhaka street", -23, false)]
    [InlineData(1, "GasLeak", 2, "Dhaka street", -23, false)]
    [InlineData(1, "GasLeak", 1, "Another street", -23, false)]
    [InlineData(1, "GasLeak", 1, "Dhaka street", -24, false)]
    [InlineData(1, "GasLeak", 1, "Dhaka street", 1, false)]
    public async Task DuplicateWarningRespectsIdentityTypeLocationAndWindow(int userId, string type, int area, string location, int hours, bool duplicate)
    {
        await SeedReports(1, clock.GetUtcNow().UtcDateTime.AddHours(hours), userId, Enum.Parse<AnomalyType>(type), area, location);
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<UtilityAnomalyCreated>())!;
        Assert.Equal(duplicate, created.IsPossibleDuplicate);
        if (duplicate) { Assert.Equal(1, created.DuplicateOfReportId); Assert.NotNull(created.Warning); }
        else { Assert.Null(created.DuplicateOfReportId); Assert.Null(created.Warning); }
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var report = await db.Reports.SingleAsync(r => r.Id == created.Id);
        Assert.Equal(duplicate, report.IsPossibleDuplicate); Assert.Equal(created.DuplicateOfReportId, report.DuplicateOfReportId);
        Assert.Equal(2, await db.Reports.CountAsync());
    }
    [Fact]
    public async Task JustInside24HoursWarnsAndSelectsMostRecentMatch()
    {
        await SeedReports(1, clock.GetUtcNow().UtcDateTime.AddHours(-24).AddTicks(1));
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        var first = (await response.Content.ReadFromJsonAsync<UtilityAnomalyCreated>())!;
        Assert.True(first.IsPossibleDuplicate); Assert.Equal(1, first.DuplicateOfReportId);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var next = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        var second = (await next.Content.ReadFromJsonAsync<UtilityAnomalyCreated>())!;
        Assert.True(second.IsPossibleDuplicate); Assert.Equal(first.Id, second.DuplicateOfReportId);
    }
    [Fact]
    public async Task OtherUsersAndGenericReportsDoNotConsumeQuotaOrProduceUtilityWarnings()
    {
        await SeedReports(10, clock.GetUtcNow().UtcDateTime, userId: 2);
        await SeedReports(10, clock.GetUtcNow().UtcDateTime, category: ReportCategory.Other);
        using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<UtilityAnomalyCreated>())!.IsPossibleDuplicate);
    }
    [Fact]
    public async Task FailedRequestsDoNotConsumeRemainingQuota()
    {
        await SeedReports(9, clock.GetUtcNow().UtcDateTime);
        using var invalid = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form(bytes: [77, 90]));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var accepted = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form());
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode); Assert.Single(Photos());
    }
    [Fact]
    public async Task ConcurrentSubmissionsCannotExceedTenAcceptedReports()
    {
        var statuses = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            using var response = await Send(HttpMethod.Post, "/api/reports/utility-anomalies", Form()); return response.StatusCode;
        }));
        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests)); Assert.Equal(10, Photos().Length);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(10, await db.Reports.CountAsync()); Assert.Equal(10, await db.ReportStatusHistories.CountAsync());
        Assert.Equal(9, await db.Reports.CountAsync(r => r.IsPossibleDuplicate));
    }
    private sealed class SubmissionTestClock : TimeProvider
    {
        private long ticks = DateTime.UtcNow.Ticks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
    }
    public async Task DisposeAsync()
    {
        client?.Dispose(); if (app != null) await app.DisposeAsync();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
