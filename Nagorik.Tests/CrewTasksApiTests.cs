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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Nagorik.Api;
using Nagorik.Api.Controllers;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

// Runs real routing, JSON validation, JWT authentication, and role authorization over localhost.
public class CrewTasksApiTests : IAsyncLifetime
{
    private const string Key = "crew-api-tests-signing-key-with-at-least-32-bytes";
    private SqliteConnection _connection = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<ICrewTaskNotifier> _crewNotifications = new();
    private readonly PauseReportSaveInterceptor _saveGate = new();

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
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection).AddInterceptors(_saveGate));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CrewTasksApiController).Assembly);
        builder.Services.AddScoped<ReportStatusWorkflowService>();
        builder.Services.AddScoped<ResidentReportStatusNotifier>();
        builder.Services.AddScoped<DispatcherTaskStatusNotifier>();
        builder.Services.AddScoped<CrewAssignmentService>();
        builder.Services.AddSingleton(_crewNotifications.Object);
        builder.Services.AddSingleton(_notifications.Object);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false, ValidateAudience = false,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                ValidateLifetime = true
            };
        });
        builder.Services.AddAuthorization();
        _app = builder.Build();
        _app.UseStaticFiles();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        _app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await using (var db = CreateDb())
        {
            await db.Database.EnsureCreatedAsync();
            TestDb.AddUser(db, 1);
            TestDb.AddUser(db, 2);
            TestDb.AddUser(db, 3);
            db.Crews.AddRange(new Crew { Id = 10, UserId = 2, Name = "Assigned crew" },
                new Crew { Id = 20, UserId = 3, Name = "Other crew" });
            db.Reports.AddRange(
                new Report { Id = 1, UserId = 1, AssignedCrewId = 10, Status = ReportStatus.Assigned,
                    Title = "Crew task <script>alert(1)</script>", Description = "Repair the road", Location = "Dhaka" },
                new Report { Id = 2, UserId = 1, Status = ReportStatus.Approved, Authority = Authority.CityCorporation,
                    Title = "Unassigned private report" });
            await db.SaveChangesAsync();
        }
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    [Fact]
    public async Task SimultaneousAssignments_CreateOneAssignmentAndHistoryAndNotifyOnce()
    {
        Authenticate("1", "Dispatcher");
        _saveGate.Arm();
        var firstRequest = _client.PostAsJsonAsync("/api/dispatch/reports/2/assign", new { crewId = 10, confirmBusy = true });
        try
        {
            await _saveGate.Paused.WaitAsync(TimeSpan.FromSeconds(10));
            using var winner = await _client.PostAsJsonAsync("/api/dispatch/reports/2/assign", new { crewId = 20, confirmBusy = true });
            Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        }
        finally { _saveGate.Resume(); }
        using var loser = await firstRequest;
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        await using var db = CreateDb();
        Assert.Equal(20, (await db.Reports.SingleAsync(r => r.Id == 2)).AssignedCrewId);
        Assert.Equal(20, (await db.CrewAssignments.SingleAsync()).CrewId);
        Assert.Equal(ReportStatus.Assigned, (await db.ReportStatusHistories.SingleAsync()).Status);
        _notifications.Verify(n => n.SendToUserAsync("1", It.IsAny<string>(), It.IsAny<string>(),
            "/Reports/Detail/2", null, default), Times.Once);
        _crewNotifications.Verify(n => n.NotifyAssignedAsync(It.IsAny<Report>(), It.IsAny<Crew>()), Times.Once);
    }

    [Fact]
    public async Task SimultaneousUpdates_CommitAndNotifyOnceAndReturn409ForLosingRequest()
    {
        await SeedDispatcherAssignments();
        Authenticate("2", "Crew");
        _saveGate.Arm();
        var firstRequest = _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        try
        {
            await _saveGate.Paused.WaitAsync(TimeSpan.FromSeconds(10));
            using var winner = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
            Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        }
        finally { _saveGate.Resume(); }
        using var loser = await firstRequest;
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        Assert.Equal("application/problem+json", loser.Content.Headers.ContentType?.MediaType);
        Assert.Contains("assignment changed", await loser.Content.ReadAsStringAsync());
        await AssertState(ReportStatus.InProgress, 1);
        foreach (var user in new[] { "1", "5" })
            _notifications.Verify(n => n.SendToUserAsync(user, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), null, default), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssignmentChangeOrDeletionDuringUpdate_Returns409WithoutHistoryOrNotifications(bool delete)
    {
        await SeedDispatcherAssignments();
        Authenticate("2", "Crew");
        _saveGate.Arm();
        var request = _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        try
        {
            await _saveGate.Paused.WaitAsync(TimeSpan.FromSeconds(10));
            await using var db = CreateDb();
            if (delete)
                await db.Reports.Where(r => r.Id == 1).ExecuteDeleteAsync();
            else
                await db.Reports.Where(r => r.Id == 1).ExecuteUpdateAsync(set => set.SetProperty(r => r.AssignedCrewId, 20));
        }
        finally { _saveGate.Resume(); }
        using var response = await request;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var persisted = CreateDb();
        Assert.Empty(await persisted.ReportStatusHistories.ToListAsync());
        if (!delete)
        {
            var report = await persisted.Reports.SingleAsync(r => r.Id == 1);
            Assert.Equal(20, report.AssignedCrewId);
            Assert.Equal(ReportStatus.Assigned, report.Status);
        }
        _notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CrewUpdates_NotifyLatestAssigningDispatcherOnceAndLinkToWorkingDetail()
    {
        await SeedDispatcherAssignments();
        var verifiedCommittedNotifications = 0;
        _notifications.Setup(n => n.SendToUserAsync("5", "Crew task status updated",
                It.IsAny<string>(), "/Dispatch/Report/1", null, default))
            .Returns(async (string user, string title, string body, string url, DateTime? timestamp, CancellationToken ct) =>
            {
                await using var db = CreateDb();
                var status = (await db.Reports.SingleAsync(r => r.Id == 1)).Status;
                Assert.Contains(status.ToString(), body);
                Assert.Contains("Assigned crew (crew #10)", body);
                Assert.Contains("R-000001", body);
                Assert.True(await db.ReportStatusHistories.AnyAsync(h => h.ReportId == 1 && h.Status == status));
                verifiedCommittedNotifications++;
                return 1;
            });
        Authenticate("2", "Crew");
        using var invalid = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        using var start = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        start.EnsureSuccessStatusCode();
        using var repeat = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        using var finish = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "Completed" });
        finish.EnsureSuccessStatusCode();
        Assert.Equal(2, verifiedCommittedNotifications);
        _notifications.Verify(n => n.SendToUserAsync("5", "Crew task status updated", It.IsAny<string>(),
            "/Dispatch/Report/1", null, default), Times.Exactly(2));
        _notifications.Verify(n => n.SendToUserAsync("4", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), null, default), Times.Never);
        Authenticate("5", "Dispatcher");
        var detail = await _client.GetStringAsync("/Dispatch/Report/1");
        Assert.Contains("Completed", detail);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("5")]
    public async Task CrewUpdate_DeliveryFailuresPreserveTaskAndOtherRecipients(string failingUser)
    {
        await SeedDispatcherAssignments();
        _notifications.Setup(n => n.SendToUserAsync(failingUser, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), null, default)).ThrowsAsync(new InvalidOperationException("Push down"));
        Authenticate("2", "Crew");
        using var response = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        response.EnsureSuccessStatusCode();
        await AssertState(ReportStatus.InProgress, 1);
        foreach (var user in new[] { "1", "5" })
            _notifications.Verify(n => n.SendToUserAsync(user, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), null, default), Times.Once);
    }

    [Fact]
    public async Task CrewUpdate_WrongCrewAndSaveFailuresDoNotNotifyDispatcher()
    {
        await SeedDispatcherAssignments();
        Authenticate("3", "Crew");
        using var wrongCrew = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.NotFound, wrongCrew.StatusCode);
        Authenticate("2", "Crew");
        await using (var db = CreateDb())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_report_update BEFORE UPDATE ON Reports BEGIN SELECT RAISE(ABORT, 'test save failure'); END;");
        using var failure = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        await AssertUnchanged();
    }

    private async Task SeedDispatcherAssignments()
    {
        await using var db = CreateDb();
        TestDb.AddUser(db, 4);
        TestDb.AddUser(db, 5);
        db.CrewAssignments.AddRange(
            new CrewAssignment { ReportId = 1, CrewId = 20, DispatcherId = 4, AssignedAtUtc = DateTime.UtcNow.AddHours(-2) },
            new CrewAssignment { ReportId = 1, CrewId = 10, DispatcherId = 5, AssignedAtUtc = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData("1", "Resident", HttpStatusCode.Forbidden)]
    [InlineData("1", "Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("invalid", "Crew", HttpStatusCode.Unauthorized)]
    [InlineData("3", "Crew", HttpStatusCode.NotFound)]
    public async Task TaskPage_EnforcesRoleAndOwnership(string? userId, string? role, HttpStatusCode expected)
    {
        Authenticate(userId, role);
        using var response = await _client.GetAsync("/crew/tasks/1");
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task TaskPages_ShowOnlyOwnTasksAndEscapeReportContent()
    {
        Authenticate("2", "Crew");
        var list = await _client.GetStringAsync("/crew/tasks");
        Assert.Contains("/crew/tasks/1", list);
        Assert.Contains("Crew task &lt;script&gt;", list);
        Assert.DoesNotContain("Unassigned private report", list);
        var detail = await _client.GetStringAsync("/crew/tasks/1");
        Assert.Contains("Repair the road", detail);
        Assert.Contains("Dhaka", detail);
        Assert.Contains("No status history is available yet.", detail);
        Assert.Contains("data-next-status=\"InProgress\"", detail);
        Assert.Contains("Crew task &lt;script&gt;", detail);
        Assert.DoesNotContain("<script>alert(1)</script>", detail);

        Authenticate("3", "Crew");
        var empty = await _client.GetStringAsync("/crew/tasks");
        Assert.Contains("You have no assigned tasks yet.", empty);
        Assert.DoesNotContain("/crew/tasks/1", empty);
        using var unassigned = await _client.GetAsync("/crew/tasks/2");
        Assert.Equal(HttpStatusCode.NotFound, unassigned.StatusCode);
    }

    [Fact]
    public async Task TaskPage_RefreshesNextActionAndHistoryThroughCompletion()
    {
        Authenticate("2", "Crew");
        using var started = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        started.EnsureSuccessStatusCode();
        var progress = await _client.GetStringAsync("/crew/tasks/1");
        Assert.Contains("data-next-status=\"Completed\"", progress);
        Assert.Contains("In Progress", progress);
        Assert.Contains("by user #2", progress);
        using var completed = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "Completed" });
        completed.EnsureSuccessStatusCode();
        var finished = await _client.GetStringAsync("/crew/tasks/1");
        Assert.Contains("No further status updates are available.", finished);
        Assert.DoesNotContain("id=\"update-task-status\"", finished);
        Assert.Contains("Completed", finished);
    }

    [Theory]
    [InlineData(null, null, HttpStatusCode.Unauthorized)]
    [InlineData("1", "Resident", HttpStatusCode.Forbidden)]
    [InlineData("1", "Dispatcher", HttpStatusCode.Forbidden)]
    [InlineData("1", "Official", HttpStatusCode.Forbidden)]
    [InlineData("invalid", "Crew", HttpStatusCode.Unauthorized)]
    [InlineData("0", "Crew", HttpStatusCode.Unauthorized)]
    [InlineData("3", "Crew", HttpStatusCode.NotFound)]
    [InlineData("1", "Crew", HttpStatusCode.NotFound)]
    public async Task UpdateStatus_EnforcesAuthenticationRoleAndAssignment(string? userId, string? role, HttpStatusCode expected)
    {
        Authenticate(userId, role);
        using var response = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(expected, response.StatusCode);
        await AssertUnchanged();
    }

    [Theory]
    [InlineData("{}")] // missing status
    [InlineData("null")]
    [InlineData("{\"status\":null}")]
    [InlineData("{\"status\":999}")]
    [InlineData("{\"status\":\"999\"}")]
    [InlineData("{\"status\":\"Assigned\"}")]
    [InlineData("{\"status\":\"Rejected\"}")]
    [InlineData("{\"status\":\"Resolved\"}")]
    [InlineData("{\"status\":\"\"}")]
    [InlineData("{\"status\":\"   \"}")]
    [InlineData("{\"status\":\"inprogress\"}")]
    [InlineData("{\"status\":true}")]
    [InlineData("{\"status\":[]}")]
    [InlineData("{\"status\":\"InProgress\"")]
    public async Task UpdateStatus_RejectsInvalidCommands(string json)
    {
        Authenticate("2", "Crew");
        using var response = await _client.PatchAsync("/api/crew/tasks/1/status", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchanged();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateStatus_InvalidTaskIdReturnsValidationProblem(int id)
    {
        Authenticate("2", "Crew");
        using var response = await _client.PatchAsJsonAsync($"/api/crew/tasks/{id}/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertUnchanged();
    }

    [Theory]
    [InlineData(ReportStatus.Completed)]
    [InlineData(ReportStatus.Rejected)]
    [InlineData(ReportStatus.Pending)]
    [InlineData(ReportStatus.Approved)]
    public async Task UpdateStatus_NonActiveTaskReturnsConflictWithoutWrites(ReportStatus status)
    {
        await using (var db = CreateDb())
        {
            var report = await db.Reports.SingleAsync(r => r.Id == 1);
            report.Status = status;
            await db.SaveChangesAsync();
        }
        Authenticate("2", "Crew");
        using var response = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await using var persisted = CreateDb();
        Assert.Equal(status, (await persisted.Reports.SingleAsync(r => r.Id == 1)).Status);
        Assert.Empty(await persisted.ReportStatusHistories.ToListAsync());
        _notifications.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(999)]
    [InlineData(2)]
    public async Task UpdateStatus_MissingAndUnassignedTasksReturn404(int id)
    {
        Authenticate("2", "Crew");
        using var response = await _client.PatchAsJsonAsync($"/api/crew/tasks/{id}/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnchanged();
    }

    [Fact]
    public async Task UpdateStatus_AdvancesTaskAtomicallyAndRejectsSkippedRepeatedAndBackwardTransitions()
    {
        Authenticate("2", "Crew");
        using var skipped = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.Conflict, skipped.StatusCode);
        await AssertUnchanged();

        // Extra identity fields cannot replace the authenticated crew user.
        using var started = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status",
            new { status = "InProgress", crewId = 20, userId = 3 });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        await AssertState(ReportStatus.InProgress, 1);
        using var repeated = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        await AssertState(ReportStatus.InProgress, 1);
        using var completed = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        await AssertState(ReportStatus.Completed, 2);
        using var backwards = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.Conflict, backwards.StatusCode);
        await AssertState(ReportStatus.Completed, 2);
        await using var db = CreateDb();
        var assignments = new CrewAssignmentService(db, Mock.Of<ICrewTaskNotifier>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CrewAssignmentService>.Instance,
            ResidentNotificationTestHelper.Create());
        Assert.False((await assignments.GetCrewsAsync()).Single(c => c.Id == 10).IsBusy);
        _notifications.Verify(n => n.SendToUserAsync("1", It.IsAny<string>(), It.IsAny<string>(),
            "/Reports/Detail/1", null, default), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateStatus_SaveFailureRollsBackStatusAndHistoryWithoutNotifying()
    {
        Authenticate("2", "Crew");
        await using (var db = CreateDb())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_report_update BEFORE UPDATE ON Reports BEGIN SELECT RAISE(ABORT, 'test save failure'); END;");
        using var response = await _client.PatchAsJsonAsync("/api/crew/tasks/1/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertUnchanged();
    }

    private AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private async Task AssertUnchanged()
    {
        await using var db = CreateDb();
        Assert.Equal(ReportStatus.Assigned, (await db.Reports.SingleAsync(r => r.Id == 1)).Status);
        Assert.Equal(ReportStatus.Approved, (await db.Reports.SingleAsync(r => r.Id == 2)).Status);
        Assert.Empty(await db.ReportStatusHistories.ToListAsync());
        _notifications.VerifyNoOtherCalls();
    }

    private async Task AssertState(ReportStatus status, int count)
    {
        await using var db = CreateDb();
        var report = await db.Reports.SingleAsync(r => r.Id == 1);
        Assert.Equal(status, report.Status);
        Assert.Equal(10, report.AssignedCrewId);
        var history = await db.ReportStatusHistories.OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(count, history.Count);
        Assert.All(history, h => Assert.Equal(2, h.ChangedByUserId));
        Assert.Equal(status, history.Last().Status);
        Assert.Equal(ReportStatus.Assigned, history[0].PreviousStatus);
        if (count == 2) Assert.Equal(ReportStatus.InProgress, history[1].PreviousStatus);
    }

    private void Authenticate(string? userId, string? role)
    {
        if (userId == null) return;
        var token = new JwtSecurityToken(claims: new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role!)
        }, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials:
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app != null) await _app.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }
}
