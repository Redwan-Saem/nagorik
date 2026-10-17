using System.Data.Common;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Controllers;
using Nagorik.Api.Data;
using Nagorik.Api.Services;
using Xunit.Abstractions;

[CollectionDefinition("Official report performance", DisableParallelization = true)]
public class OfficialReportPerformanceCollection { }

// File-backed SQLite and real localhost HTTP. This collection runs without competing xUnit tests.
[Collection("Official report performance")]
public class OfficialReportListPerformanceTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const int Warmups = 2;
    private const int Trials = 5;
    private const double BudgetMilliseconds = 2_000;
    private const string Key = "official-list-performance-isolated-test-signing-key";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nagorik-official-performance-" + Guid.NewGuid().ToString("N"));
    private readonly SqlCapture _capture = new();
    private string _connectionString = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_directory, "reports.sqlite"), Pooling = false, ForeignKeys = true
        }.ToString();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
            EnvironmentName = Environments.Production
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connectionString).AddInterceptors(_capture));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(OfficialReportsApiController).Assembly);
        builder.Services.AddScoped<OfficialReportQueryService>();
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
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            var seedEnvironment = new Mock<IHostEnvironment>();
            seedEnvironment.SetupGet(e => e.EnvironmentName).Returns(Environments.Development);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevelopmentData:SeedPerformanceReports"] = "true"
            }).Build();
            Assert.Equal(10_000, await DevelopmentReportSeeder.SeedAsync(db, seedEnvironment.Object, configuration));
            Assert.Equal(10_000, await db.Reports.CountAsync());
        }
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(30) };
        var token = new JwtSecurityToken(claims:
            [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "Official")],
            expires: DateTime.UtcNow.AddMinutes(20), signingCredentials:
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Add("Cookie", "auth_token=" + new JwtSecurityTokenHandler().WriteToken(token));
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task OfficialListLoadsUnderTwoSecondsWith10000ReportsAcrossRepeatedTrials()
    {
        var scenarios = new[]
        {
            new Scenario("Newest first", "sort=date&direction=desc", ExpectedIndex: "IX_Reports_CreatedAt_Id"),
            new Scenario("Oldest first", "sort=date&direction=asc", ExpectedIndex: "IX_Reports_CreatedAt_Id"),
            new Scenario("Status ascending", "sort=status&direction=asc", ExpectedIndex: "IX_Reports_Status_CreatedAt_Id"),
            new Scenario("Status descending", "sort=status&direction=desc"),
            new Scenario("Urgency ascending", "sort=urgency&direction=asc"),
            new Scenario("Urgency descending", "sort=urgency&direction=desc", ExpectedIndex: "IX_Reports_Urgency_CreatedAt_Id"),
            new Scenario("Area and single category", "areaId=1&categories=RoadDamage", Area: 1, Categories: ["RoadDamage"]),
            new Scenario("Area and multiple categories", "areaId=1&categories=RoadDamage&categories=WaterLogging", Area: 1, Categories: ["RoadDamage", "WaterLogging"]),
            new Scenario("Area only", "areaId=2", Area: 2),
            new Scenario("Last page", "page=500&sort=date&direction=desc", Page: 500, ExpectedIndex: "IX_Reports_CreatedAt_Id")
        };
        string sqliteVersion;
        await using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT sqlite_version();";
            sqliteVersion = (string)(await command.ExecuteScalarAsync())!;
        }
        var results = new List<ScenarioResult>();
        foreach (var scenario in scenarios)
        {
            int expectedTotal;
            await using (var scope = _app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Independent count using the fixture's stored values, outside the measured interval.
                var groups = await db.Reports.GroupBy(r => new { r.AreaId, r.Category })
                    .Select(g => new { g.Key.AreaId, g.Key.Category, Count = g.Count() }).ToListAsync();
                expectedTotal = groups.Where(g => (!scenario.Area.HasValue || g.AreaId == scenario.Area) &&
                    (scenario.Categories == null || scenario.Categories.Contains(g.Category.ToString())))
                    .Sum(g => g.Count);
            }
            _capture.Enabled = true;
            var warmups = new List<Trial>();
            for (var i = 0; i < Warmups; i++) warmups.Add(await MeasureAsync(scenario, expectedTotal));
            var trials = new List<Trial>();
            for (var i = 0; i < Trials; i++) trials.Add(await MeasureAsync(scenario, expectedTotal));
            _capture.Enabled = false;
            var countSql = _capture.Commands.Single(c => c.Text.Contains("COUNT(*)", StringComparison.Ordinal));
            var pageSql = _capture.Commands.Single(c => c.Text.Contains("LIMIT", StringComparison.Ordinal));
            var countPlan = await ExplainAsync(countSql);
            var pagePlan = await ExplainAsync(pageSql);
            if (scenario.ExpectedIndex != null)
                Assert.Contains(pagePlan, line => line.Contains(scenario.ExpectedIndex, StringComparison.Ordinal));
            var sorted = trials.Select(t => t.TotalMilliseconds).Order().ToArray();
            var result = new ScenarioResult(scenario.Name, scenario.Query, expectedTotal, warmups, trials,
                sorted[sorted.Length / 2], sorted[^1], countSql, countPlan, pageSql, pagePlan);
            results.Add(result);
            output.WriteLine($"{scenario.Name}: median {result.MedianMilliseconds:F2} ms, max {result.MaximumMilliseconds:F2} ms; {expectedTotal} matches");
        }
        var path = Environment.GetEnvironmentVariable("NAGORIK_PERF_OUTPUT") ??
            Path.Combine(Path.GetTempPath(), "nagorik-report-list-performance-" + Guid.NewGuid().ToString("N") + ".json");
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var report = new
        {
            MeasuredAtUtc = DateTime.UtcNow,
            Environment = new
            {
                OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), LogicalProcessors = Environment.ProcessorCount,
                GcAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                EfCore = typeof(DbContext).Assembly.GetName().Version!.ToString(), Sqlite = sqliteVersion,
#if DEBUG
                Configuration = "Debug"
#else
                Configuration = "Release"
#endif
            },
            Database = "Isolated temporary file-backed SQLite; migrations applied; pooling disabled",
            ReportCount = 10_000, PageSize = 20, WarmupsPerScenario = Warmups, TrialsPerScenario = Trials,
            BudgetMilliseconds,
            Measurement = "Sequential authenticated GET /official/reports?query (HTML body read), then GET /api/reports?query (JSON parsed); includes localhost HTTP, routing, auth, SQL and Razor; excludes browser scripts, paint and asset downloads",
            Results = results
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        output.WriteLine("Performance evidence: " + path);
        Assert.All(results.SelectMany(r => r.Trials), trial => Assert.True(trial.TotalMilliseconds < BudgetMilliseconds,
            $"List HTML + API took {trial.TotalMilliseconds:F2} ms; budget is strictly below {BudgetMilliseconds} ms."));
        await using var finalScope = _app.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(10_000, await finalDb.Reports.CountAsync());
        Assert.Empty(await finalDb.ReportStatusHistories.ToListAsync());
        Assert.Empty(await finalDb.NotificationLogs.ToListAsync());
    }

    private async Task<Trial> MeasureAsync(Scenario scenario, int expectedTotal)
    {
        _capture.Commands.Clear();
        var total = Stopwatch.StartNew();
        using var shell = await _client.GetAsync("/official/reports?" + scenario.Query);
        shell.EnsureSuccessStatusCode();
        var html = await shell.Content.ReadAsStringAsync();
        var shellMs = total.Elapsed.TotalMilliseconds;
        using var response = await _client.GetAsync("/api/reports?" + scenario.Query);
        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<OfficialReportPage>())!;
        total.Stop();
        Assert.Contains("id=\"reports-rows\"", html);
        Assert.Equal(expectedTotal, page.TotalCount); Assert.Equal(scenario.Page, page.Page);
        Assert.Equal(20, page.PageSize); Assert.Equal((expectedTotal + 19) / 20, page.TotalPages);
        Assert.Equal(Math.Min(20, Math.Max(0, expectedTotal - (scenario.Page - 1) * 20)), page.Items.Count);
        Assert.Equal(page.Items.Count, page.Items.Select(r => r.Id).Distinct().Count());
        if (scenario.Area.HasValue) Assert.All(page.Items, r => Assert.Equal(scenario.Area, r.AreaId));
        if (scenario.Categories != null) Assert.All(page.Items, r => Assert.Contains(r.Category, scenario.Categories));
        Assert.Equal(3, _capture.Commands.Count); // Area options + filtered count + bounded report projection.
        var pageSql = _capture.Commands.Single(c => c.Text.Contains("LIMIT", StringComparison.Ordinal));
        Assert.Contains("OFFSET", pageSql.Text);
        Assert.DoesNotContain("\"Description\"", pageSql.Text);
        Assert.DoesNotContain("\"PasswordHash\"", pageSql.Text);
        return new Trial(shellMs, total.Elapsed.TotalMilliseconds - shellMs, total.Elapsed.TotalMilliseconds);
    }

    private async Task<List<string>> ExplainAsync(CommandSnapshot snapshot)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + snapshot.Text;
        foreach (var parameter in snapshot.Parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        return plan;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null) await _app.DisposeAsync();
        if (Directory.Exists(_directory))
        {
            foreach (var file in Directory.EnumerateFiles(_directory)) File.Delete(file);
            Directory.Delete(_directory);
        }
    }

    private sealed record Scenario(string Name, string Query, int Page = 1, int? Area = null,
        string[]? Categories = null, string? ExpectedIndex = null);
    private sealed record Trial(double HtmlMilliseconds, double ApiMilliseconds, double TotalMilliseconds);
    private sealed record ParameterSnapshot(string Name, object? Value);
    private sealed record CommandSnapshot(string Text, List<ParameterSnapshot> Parameters);
    private sealed record ScenarioResult(string Name, string Query, int MatchingReports, List<Trial> Warmups,
        List<Trial> Trials, double MedianMilliseconds, double MaximumMilliseconds,
        CommandSnapshot CountSql, List<string> CountPlan, CommandSnapshot PageSql, List<string> PagePlan);
    private sealed class SqlCapture : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public List<CommandSnapshot> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (Enabled) Commands.Add(new CommandSnapshot(command.CommandText,
                command.Parameters.Cast<DbParameter>().Select(p => new ParameterSnapshot(p.ParameterName, p.Value is DBNull ? null : p.Value)).ToList()));
            return ValueTask.FromResult(result);
        }
    }
}
