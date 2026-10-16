using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Controllers;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

public class OfficialReportListApiTests : IAsyncLifetime
{
    private const string Key = "official-report-api-tests-signing-key-at-least-32-bytes";
    private SqliteConnection _connection = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly List<Report> _reports = [];
    private readonly QueryCapture _queries = new();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection).AddInterceptors(_queries));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(OfficialReportsApiController).Assembly);
        builder.Services.AddScoped<OfficialReportQueryService>();
        builder.Services.AddSingleton(Mock.Of<IReportService>());
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
            };
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Token ??= context.Request.Cookies["auth_token"];
                    return Task.CompletedTask;
                }
            };
        });
        builder.Services.AddAuthorization();
        _app = builder.Build();
        _app.UseAuthentication(); _app.UseAuthorization(); _app.MapControllers();
        _app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
        {
            await db.Database.EnsureCreatedAsync();
            TestDb.AddUser(db, 1); TestDb.AddUser(db, 2);
            var categories = new[] { ReportCategory.RoadDamage, ReportCategory.WaterLogging, ReportCategory.PowerOutage };
            for (var i = 1; i <= 65; i++)
            {
                var report = new Report
                {
                    Id = i, UserId = i % 2 + 1, Title = $"Report {i}", Description = $"Private detail {i}",
                    Category = categories[i % 3], AreaId = i % 7 == 0 ? null : i % 2 + 1,
                    Status = (ReportStatus)(i % 6), Urgency = (ReportUrgency)(i % 4),
                    CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i / 3)
                };
                _reports.Add(report); db.Reports.Add(report);
            }
            await db.SaveChangesAsync();
        }
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
        _queries.Sql.Clear();
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("Official", HttpStatusCode.OK)]
    [InlineData("CityOfficial", HttpStatusCode.OK)]
    public async Task ReportListPageEnforcesRolesAndRendersTableOrPermissionMessage(string? role, HttpStatusCode expected)
    {
        if (role != null) Authenticate(role);
        using var response = await _client.GetAsync("/official/reports");
        Assert.Equal(expected, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        if (expected == HttpStatusCode.Forbidden)
        {
            Assert.Contains("You do not have permission", html);
            Assert.DoesNotContain("id=\"reports-rows\"", html);
        }
        if (expected == HttpStatusCode.OK)
        {
            Assert.Contains("id=\"reports-rows\"", html);
            Assert.Contains("data-sort=\"urgency\"", html);
            Assert.Contains("Next page", html);
            Assert.Contains("official-reports.js", html);
            Assert.Contains("Loading reports", html);
            Assert.Contains("href=\"/official/reports\"", html);
            Assert.Contains("id=\"reports-filters\"", html);
            Assert.Contains("Apply filters", html);
            Assert.Contains("Clear filters", html);
            Assert.Contains("No reports match your filters.", html);
            foreach (var category in ReportLookups.Categories)
                Assert.Contains($"value=\"{category.Category}\" data-report-category", html);
            Assert.Contains("Dhanmondi", html);
            Assert.Contains("Mirpur", html);
            Assert.Contains("Gulshan", html);
            Assert.Contains("ReportAreas", Assert.Single(_queries.Sql));
        }
        else Assert.Empty(_queries.Sql);
    }

    [Fact]
    public async Task ReportFilterControlsUseCurrentDatabaseAreasAndEncodeTheirNames()
    {
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
        {
            db.ReportAreas.Add(new ReportArea { Id = 10, Name = "<Area & zone>" });
            await db.SaveChangesAsync();
        }
        Authenticate("Official");
        _queries.Sql.Clear();
        var html = await _client.GetStringAsync("/official/reports?categories=RoadDamage&areaId=10");
        Assert.Contains("<option value=\"10\">&lt;Area &amp; zone&gt;</option>", html);
        Assert.DoesNotContain("<Area & zone>", html);
        Assert.Contains("ReportAreas", Assert.Single(_queries.Sql));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("official", HttpStatusCode.Forbidden)]
    [InlineData("Official", HttpStatusCode.OK)]
    [InlineData("CityOfficial", HttpStatusCode.OK)]
    public async Task OfficialDetailPageEnforcesRolesBeforeReadingReports(string? role, HttpStatusCode expected)
    {
        if (role != null) Authenticate(role, cookie: true);
        using var response = await _client.GetAsync("/official/reports/1");
        Assert.Equal(expected, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        if (expected == HttpStatusCode.OK)
        {
            Assert.Contains("Report R-000001", html);
            Assert.Contains("Private detail 1", html);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            Assert.Equal(2, _queries.Sql.Count);
        }
        else
        {
            Assert.DoesNotContain("Private detail 1", html);
            Assert.Empty(_queries.Sql);
            if (expected == HttpStatusCode.Forbidden) Assert.Contains("You do not have permission", html);
        }
    }

    [Fact]
    public async Task OfficialDetailPageRendersEscapedReportPhotoLocationCrewAndOrderedUtcHistory()
    {
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
        {
            TestDb.AddUser(db, 3);
            db.Crews.Add(new Crew { Id = 10, UserId = 3, Name = "Road crew" });
            var report = await db.Reports.SingleAsync(r => r.Id == 1);
            report.Title = "Road <script>alert(1)</script>";
            report.Description = "First line\n<img src=x onerror=alert(2)>";
            report.Location = "Road 5, Dhaka"; report.Latitude = 23.81; report.Longitude = 90.41;
            report.Category = ReportCategory.RoadDamage; report.AreaId = 1;
            report.Authority = Authority.CityCorporation; report.Status = ReportStatus.InProgress;
            report.Urgency = ReportUrgency.High; report.AssignedCrewId = 10;
            report.ImageUrl = "/uploads/reports/issue.jpg";
            db.ReportStatusHistories.AddRange(
                new ReportStatusHistory { ReportId = 1, Status = ReportStatus.InProgress, PreviousStatus = ReportStatus.Assigned,
                    ChangedByUserId = 3, ChangedAtUtc = new DateTime(2026, 10, 2, 11, 30, 0, DateTimeKind.Utc) },
                new ReportStatusHistory { ReportId = 1, Status = ReportStatus.Pending,
                    ChangedAtUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
        }
        Authenticate("Official");
        _queries.Sql.Clear();
        var html = await _client.GetStringAsync("/official/reports/1");
        Assert.Contains("Road &lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(2)&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("class=\"official-report-description\"", html);
        Assert.Contains("Road 5, Dhaka", html); Assert.Contains("23.81000, 90.41000", html);
        Assert.Contains("Dhanmondi", html); Assert.Contains("Road damage", html);
        Assert.Contains("Road crew", html); Assert.Contains("In Progress", html); Assert.Contains("High", html);
        Assert.Contains("src=\"/uploads/reports/issue.jpg\"", html);
        Assert.Contains("alt=\"Photo of the issue reported in R-000001\"", html);
        Assert.Contains("Actor not recorded", html); Assert.Contains("by user #3", html);
        Assert.True(html.IndexOf("2026-10-01T10:00:00Z", StringComparison.Ordinal) <
                    html.IndexOf("2026-10-02T11:30:00Z", StringComparison.Ordinal));
        Assert.Contains("2026-10-02 11:30 UTC", html);
        Assert.Equal(2, _queries.Sql.Count);
        Assert.DoesNotContain(_queries.Sql, sql => sql.Contains("Email") || sql.Contains("PasswordHash"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//example.com/uploads/issue.jpg")]
    [InlineData("https://example.com/issue.jpg")]
    public async Task OfficialDetailPageHandlesMissingOrUnsupportedPhotoAndOptionalFields(string? photo)
    {
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
            await db.Reports.Where(r => r.Id == 7).ExecuteUpdateAsync(s => s.SetProperty(r => r.ImageUrl, photo));
        Authenticate("Official");
        var html = await _client.GetStringAsync("/official/reports/7");
        Assert.Contains("Unclassified", html); Assert.Contains("Not assigned", html);
        Assert.Contains("No report photo is available.", html);
        Assert.Contains("No status history is available yet.", html);
        Assert.DoesNotContain("id=\"official-report-photo\"", html);
    }

    [Theory]
    [InlineData("/official/reports?categories=RoadDamage&categories=WaterLogging&areaId=1&page=3&sort=urgency&direction=asc", true)]
    [InlineData("/official/reports", true)]
    [InlineData("https://example.com", false)]
    [InlineData("//example.com", false)]
    [InlineData("/login.html", false)]
    [InlineData("/official/reports/2", false)]
    [InlineData("/official/reports.evil", false)]
    public async Task OfficialDetailBackLinkPreservesOnlyLocalOfficialListUrls(string returnUrl, bool allowed)
    {
        Authenticate("Official");
        var html = await _client.GetStringAsync("/official/reports/1?returnUrl=" + Uri.EscapeDataString(returnUrl));
        var match = System.Text.RegularExpressions.Regex.Match(html, "id=\"official-detail-back\" href=\"([^\"]+)\"");
        Assert.True(match.Success);
        Assert.Equal(allowed ? returnUrl : "/official/reports", WebUtility.HtmlDecode(match.Groups[1].Value));
    }

    [Theory]
    [InlineData(999, 1)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    public async Task MissingOfficialDetailPageReturns404WithPreservedBackLink(int id, int queries)
    {
        Authenticate("Official");
        using var response = await _client.GetAsync($"/official/reports/{id}?returnUrl=%2Fofficial%2Freports%3Fpage%3D3");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Report not found", html);
        Assert.Contains("href=\"/official/reports?page=3\"", html);
        Assert.DoesNotContain("official-history-heading", html);
        Assert.Equal(queries, _queries.Sql.Count);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("Official", HttpStatusCode.OK)]
    [InlineData("CityOfficial", HttpStatusCode.OK)]
    public async Task OfficialDetailRequiresRoleOrAlias(string? role, HttpStatusCode expected)
    {
        if (role != null) Authenticate(role);
        using var response = await _client.GetAsync("/api/official/reports/1");
        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK) Assert.Empty(_queries.Sql);
    }

    [Fact]
    public async Task OfficialDetailProjectsLocationPhotoCrewAndOrderedHistoryWithoutUserGraph()
    {
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
        {
            TestDb.AddUser(db, 3);
            db.Crews.Add(new Crew { Id = 10, UserId = 3, Name = "Road crew" });
            var report = await db.Reports.SingleAsync(r => r.Id == 1);
            report.AreaId = 1; report.Urgency = ReportUrgency.High; report.Authority = Authority.CityCorporation;
            report.Location = "Road 5, Dhaka"; report.Latitude = 23.81; report.Longitude = 90.41;
            report.ImageUrl = "/uploads/report-1.jpg"; report.AssignedCrewId = 10; report.Status = ReportStatus.InProgress;
            var time = DateTime.UtcNow.AddHours(-1);
            db.ReportStatusHistories.AddRange(
                new ReportStatusHistory { ReportId = 1, Status = ReportStatus.InProgress, PreviousStatus = ReportStatus.Assigned, ChangedByUserId = 3, ChangedAtUtc = time.AddMinutes(3) },
                new ReportStatusHistory { ReportId = 1, Status = ReportStatus.Approved, PreviousStatus = ReportStatus.Pending, ChangedByUserId = 1, ChangedAtUtc = time.AddMinutes(1) },
                new ReportStatusHistory { ReportId = 1, Status = ReportStatus.Assigned, PreviousStatus = ReportStatus.Approved, ChangedByUserId = 1, ChangedAtUtc = time.AddMinutes(1) },
                new ReportStatusHistory { ReportId = 1, Status = ReportStatus.Pending, ChangedByUserId = 2, ChangedAtUtc = time });
            await db.SaveChangesAsync();
        }
        Authenticate("Official", cookie: true);
        using var response = await _client.GetAsync("/api/official/reports/1");
        response.EnsureSuccessStatusCode();
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var detail = (await response.Content.ReadFromJsonAsync<OfficialReportDetailDto>())!;
        Assert.Equal("Private detail 1", detail.Description);
        Assert.Equal("R-000001", detail.Code);
        Assert.Equal("Road 5, Dhaka", detail.Location);
        Assert.Equal(23.81, detail.Latitude); Assert.Equal(90.41, detail.Longitude);
        Assert.Equal("/uploads/report-1.jpg", detail.ImageUrl);
        Assert.Equal("Dhanmondi", detail.AreaName); Assert.Equal("High", detail.Urgency);
        Assert.Equal("CityCorporation", detail.Authority);
        Assert.Equal(10, detail.AssignedCrewId); Assert.Equal("Road crew", detail.AssignedCrewName);
        Assert.Equal(new[] { "Pending", "Approved", "Assigned", "InProgress" }, detail.StatusHistory.Select(h => h.Status));
        Assert.Equal(new int?[] { 2, 1, 1, 3 }, detail.StatusHistory.Select(h => h.ChangedByUserId));
        Assert.Null(detail.StatusHistory[0].PreviousStatus);
        Assert.Equal("Assigned", detail.StatusHistory[3].PreviousStatus);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.TryGetProperty("user", out _));
        Assert.False(json.RootElement.TryGetProperty("passwordHash", out _));
        Assert.Equal(2, _queries.Sql.Count);
        Assert.DoesNotContain(_queries.Sql, sql => sql.Contains("PasswordHash") || sql.Contains("Email") || sql.Contains("PhoneNumber"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingOptionalDetailFieldsReturnNullAndEmptyHistory(string? photo)
    {
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
            await db.Reports.Where(r => r.Id == 7).ExecuteUpdateAsync(s => s.SetProperty(r => r.ImageUrl, photo));
        Authenticate("Official");
        var detail = (await _client.GetFromJsonAsync<OfficialReportDetailDto>("/api/official/reports/7"))!;
        Assert.Null(detail.ImageUrl); Assert.Null(detail.AreaId); Assert.Null(detail.AreaName);
        Assert.Null(detail.AssignedCrewId); Assert.Null(detail.AssignedCrewName);
        Assert.Empty(detail.StatusHistory);
    }

    [Fact]
    public async Task MissingOfficialDetailReturns404WithoutHistoryQuery()
    {
        Authenticate("Official");
        using var response = await _client.GetAsync("/api/official/reports/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(_queries.Sql);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidDetailIdReturns400WithoutQuery(int id)
    {
        Authenticate("Official");
        using var response = await _client.GetAsync($"/api/official/reports/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_queries.Sql);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Resident", HttpStatusCode.Forbidden)]
    [InlineData("Crew", HttpStatusCode.Forbidden)]
    [InlineData("Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("official", HttpStatusCode.Forbidden)]
    [InlineData("Official", HttpStatusCode.OK)]
    [InlineData("CityOfficial", HttpStatusCode.OK)]
    public async Task List_RequiresOfficialRoleOrAlias(string? role, HttpStatusCode expected)
    {
        if (role != null) Authenticate(role);
        using var response = await _client.GetAsync("/api/reports");
        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK) Assert.Empty(_queries.Sql);
    }

    [Fact]
    public async Task DefaultList_IsNewestFirstAndPaginatesInDatabaseWithTotalCount()
    {
        Authenticate("Official", cookie: true);
        var first = (await _client.GetFromJsonAsync<OfficialReportPage>("/api/reports"))!;
        Assert.Equal(20, first.PageSize); Assert.Equal(1, first.Page);
        Assert.Equal(65, first.TotalCount); Assert.Equal(4, first.TotalPages);
        Assert.Equal(_reports.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(20).Select(r => r.Id),
            first.Items.Select(r => r.Id));
        Assert.Equal("R-000065", first.Items[0].Code);
        Assert.Equal(2, _queries.Sql.Count);
        Assert.Contains(_queries.Sql, sql => sql.Contains("COUNT(*)"));
        Assert.Contains(_queries.Sql, sql => sql.Contains("LIMIT") && sql.Contains("OFFSET"));
        var second = (await _client.GetFromJsonAsync<OfficialReportPage>("/api/reports?page=2"))!;
        Assert.Empty(first.Items.Select(r => r.Id).Intersect(second.Items.Select(r => r.Id)));
        var fourth = (await _client.GetFromJsonAsync<OfficialReportPage>("/api/reports?page=4"))!;
        Assert.Equal(5, fourth.Items.Count);
        var beyond = (await _client.GetFromJsonAsync<OfficialReportPage>("/api/reports?page=5"))!;
        Assert.Empty(beyond.Items); Assert.Equal(65, beyond.TotalCount);
    }

    [Fact]
    public async Task CategoriesUseOrAndAreaUsesAndWithFilteredCount()
    {
        Authenticate("CityOfficial");
        var page = (await _client.GetFromJsonAsync<OfficialReportPage>(
            "/api/reports?categories=RoadDamage&categories=WaterLogging&areaId=1"))!;
        var expected = _reports.Where(r => r.AreaId == 1 && r.Category is ReportCategory.RoadDamage or ReportCategory.WaterLogging)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).ToList();
        Assert.Equal(expected.Count, page.TotalCount);
        Assert.Equal(expected.Take(20).Select(r => r.Id), page.Items.Select(r => r.Id));
        Assert.All(page.Items, r => Assert.Equal("Dhanmondi", r.AreaName));
        Assert.Contains(_queries.Sql, sql => sql.Contains("WHERE") && sql.Contains("Category") && sql.Contains("AreaId"));
    }

    [Theory]
    [InlineData("categories=RoadDamage")]
    [InlineData("categories=roaddamage&categories=RoadDamage")]
    public async Task CategoryNamesAreCaseInsensitiveAndDuplicatesDoNotDuplicateRows(string filter)
    {
        Authenticate("Official");
        var page = (await _client.GetFromJsonAsync<OfficialReportPage>($"/api/reports?{filter}"))!;
        Assert.Equal(_reports.Count(r => r.Category == ReportCategory.RoadDamage), page.TotalCount);
        Assert.All(page.Items, r => Assert.Equal("RoadDamage", r.Category));
        Assert.Equal(page.Items.Count, page.Items.Select(r => r.Id).Distinct().Count());
    }

    [Theory]
    [InlineData("date", "asc")]
    [InlineData("date", "desc")]
    [InlineData("status", "asc")]
    [InlineData("status", "desc")]
    [InlineData("urgency", "asc")]
    [InlineData("urgency", "desc")]
    public async Task SortsUseDocumentedOrderingAndStableTies(string sort, string direction)
    {
        Authenticate("Official");
        var page = (await _client.GetFromJsonAsync<OfficialReportPage>($"/api/reports?sort={sort}&direction={direction}"))!;
        var asc = direction == "asc";
        IOrderedEnumerable<Report> expected = sort switch
        {
            "status" => asc ? _reports.OrderBy(r => r.Status.ToString(), StringComparer.Ordinal) : _reports.OrderByDescending(r => r.Status.ToString(), StringComparer.Ordinal),
            "urgency" => asc ? _reports.OrderBy(r => r.Urgency) : _reports.OrderByDescending(r => r.Urgency),
            _ => asc ? _reports.OrderBy(r => r.CreatedAt) : _reports.OrderByDescending(r => r.CreatedAt)
        };
        expected = sort == "date" ? asc ? expected.ThenBy(r => r.Id) : expected.ThenByDescending(r => r.Id)
            : expected.ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);
        Assert.Equal(expected.Take(20).Select(r => r.Id), page.Items.Select(r => r.Id));
    }

    [Theory]
    [InlineData("date", "asc", false)]
    [InlineData("date", "desc", false)]
    [InlineData("status", "asc", false)]
    [InlineData("status", "desc", false)]
    [InlineData("urgency", "asc", false)]
    [InlineData("urgency", "desc", false)]
    [InlineData("date", "desc", true)]
    [InlineData("status", "asc", true)]
    [InlineData("urgency", "desc", true)]
    public async Task TraversingEveryPageKeepsExactOrderCountsAndMembershipAcrossTies(string sort, string direction, bool filtered)
    {
        // Collapse dates to force ties spanning page boundaries for each sort key.
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options))
        {
            var tiedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.Reports.ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, tiedDate)
                .SetProperty(r => r.AreaId, r => r.Id % 4 == 0 ? 2 : 1));
            foreach (var report in _reports)
            {
                report.CreatedAt = tiedDate;
                report.AreaId = report.Id % 4 == 0 ? 2 : 1;
            }
        }
        Authenticate("Official", cookie: true);
        var source = _reports.Where(r => !filtered || r.AreaId == 1 &&
            r.Category is ReportCategory.RoadDamage or ReportCategory.WaterLogging).ToList();
        var ascending = direction == "asc";
        // Expected ordering is calculated independently of the SQL query service.
        var expected = source.Select(r => new
        {
            Report = r,
            Key = sort == "status" ? r.Status.ToString() : sort == "urgency" ? ((int)r.Urgency).ToString() : "same-date"
        }).OrderBy(r => r.Key, ascending ? StringComparer.Ordinal : Comparer<string>.Create((a, b) => StringComparer.Ordinal.Compare(b, a)));
        var orderedIds = sort == "date" && ascending
            ? expected.ThenBy(r => r.Report.Id).Select(r => r.Report.Id).ToArray()
            : expected.ThenByDescending(r => r.Report.Id).Select(r => r.Report.Id).ToArray();
        var filter = filtered ? "&categories=RoadDamage&categories=WaterLogging&areaId=1" : "";
        var totalPages = (source.Count + 19) / 20;
        var actual = new List<int>();
        for (var pageNumber = 1; pageNumber <= totalPages; pageNumber++)
        {
            _queries.Sql.Clear();
            var url = $"/api/reports?page={pageNumber}&sort={sort}&direction={direction}{filter}";
            using var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            var page = (await response.Content.ReadFromJsonAsync<OfficialReportPage>())!;
            Assert.Equal(pageNumber, page.Page); Assert.Equal(20, page.PageSize);
            Assert.Equal(source.Count, page.TotalCount); Assert.Equal(totalPages, page.TotalPages);
            Assert.Equal(orderedIds.Skip((pageNumber - 1) * 20).Take(20), page.Items.Select(r => r.Id));
            actual.AddRange(page.Items.Select(r => r.Id));
            Assert.Equal(2, _queries.Sql.Count);
            Assert.Contains(_queries.Sql, sql => sql.Contains("LIMIT") && sql.Contains("OFFSET"));
        }
        Assert.Equal(orderedIds, actual);
        Assert.Equal(source.Count, actual.Distinct().Count());
        var lastUrl = $"/api/reports?page={totalPages}&sort={sort}&direction={direction}{filter}";
        var repeated = (await _client.GetFromJsonAsync<OfficialReportPage>(lastUrl))!;
        Assert.Equal(orderedIds.Skip((totalPages - 1) * 20), repeated.Items.Select(r => r.Id));
        var beyond = (await _client.GetFromJsonAsync<OfficialReportPage>(
            $"/api/reports?page={totalPages + 1}&sort={sort}&direction={direction}{filter}"))!;
        Assert.Empty(beyond.Items); Assert.Equal(source.Count, beyond.TotalCount); Assert.Equal(totalPages, beyond.TotalPages);
    }

    [Fact]
    public async Task HighestValidPageDoesNotOverflowAndDoesNotAlterData()
    {
        Authenticate("Official");
        var page = (await _client.GetFromJsonAsync<OfficialReportPage>("/api/reports?page=107374182"))!;
        Assert.Empty(page.Items); Assert.Equal(65, page.TotalCount); Assert.Equal(4, page.TotalPages);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        Assert.Equal(65, await db.Reports.CountAsync());
        Assert.Empty(await db.ReportStatusHistories.ToListAsync());
        Assert.Empty(await db.NotificationLogs.ToListAsync());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=2147483647")]
    [InlineData("page=abc")]
    [InlineData("sort=title")]
    [InlineData("sort=date;DROP%20TABLE%20Reports")]
    [InlineData("direction=sideways")]
    [InlineData("categories=unknown")]
    [InlineData("categories=999")]
    [InlineData("categories=4")]
    [InlineData("categories=RoadDamage&categories=WaterLogging&categories=PowerOutage&categories=Complaint&categories=Request&categories=Emergency&categories=Other&categories=RoadDamage")]
    [InlineData("areaId=-1")]
    [InlineData("areaId=0")]
    [InlineData("areaId=abc")]
    [InlineData("areaId=2147483648")]
    [InlineData("page=107374183")]
    public async Task InvalidQueryReturns400BeforeQuerying(string query)
    {
        Authenticate("Official");
        using var response = await _client.GetAsync($"/api/reports?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_queries.Sql);
    }

    [Fact]
    public async Task NoMatchesReturnEmptyItemsAndZeroCounts()
    {
        Authenticate("Official");
        var page = (await _client.GetFromJsonAsync<OfficialReportPage>("/api/reports?areaId=999"))!;
        Assert.Empty(page.Items); Assert.Equal(0, page.TotalCount); Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task ResidentDetailRemainsOwnerRestrictedAndListRemainsOfficialOnly()
    {
        Authenticate("Resident"); // User 1 owns even-numbered reports.
        using var owned = await _client.GetAsync("/api/reports/2");
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        using var other = await _client.GetAsync("/api/reports/1");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Authenticate("Official");
        using var residentDetail = await _client.GetAsync("/api/reports/2");
        Assert.Equal(HttpStatusCode.Forbidden, residentDetail.StatusCode);
    }

    private void Authenticate(string role, bool cookie = false)
    {
        var token = new JwtSecurityToken(claims: new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, role) },
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials:
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        var value = new JwtSecurityTokenHandler().WriteToken(token);
        if (cookie) _client.DefaultRequestHeaders.Add("Cookie", $"auth_token={value}");
        else _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", value);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null) await _app.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    private sealed class QueryCapture : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Sql.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
