using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nagorik.Api.Services;

public sealed class UtilityPhotoUploadTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "nagorik-photo-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalPhotoStorage storage;
    private readonly UtilityPhotoUploadService service;
    public UtilityPhotoUploadTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(root);
        env.SetupGet(e => e.WebRootPath).Returns(Path.Combine(root, "wwwroot"));
        storage = new LocalPhotoStorage(env.Object);
        service = new(storage, NullLogger<UtilityPhotoUploadService>.Instance);
    }
    private static IFormFile Photo(string name = "test.jpg", string mime = "image/jpeg", byte[]? data = null)
    {
        data ??= [255, 216, 255, 224, 0, 0, 0, 0];
        return new FormFile(new MemoryStream(data), 0, data.Length, "photos", name) { Headers = new HeaderDictionary(), ContentType = mime };
    }
    private string[] Files() => Directory.Exists(Path.Combine(root, "App_Data")) ? Directory.GetFiles(Path.Combine(root, "App_Data"), "*", SearchOption.AllDirectories) : [];

    [Theory]
    [InlineData(0)] [InlineData(4)]
    public async Task RejectsInvalidCount(int count)
    {
        await Assert.ThrowsAsync<UtilityPhotoValidationException>(() => service.SaveAndPersistAsync(Enumerable.Range(0, count).Select(_ => Photo()).ToArray(), (_, _) => Task.FromResult(1)));
        Assert.Empty(Files());
    }
    [Theory]
    [InlineData("test.exe", "image/jpeg")]
    [InlineData("test.png", "image/png")]
    [InlineData("test.jpg", "application/octet-stream")]
    [InlineData("test.jpg", "image/png")]
    public async Task RejectsMismatchedFormatsBeforeWriting(string name, string mime)
    {
        await Assert.ThrowsAsync<UtilityPhotoValidationException>(() => service.SaveAndPersistAsync([Photo(), Photo(name, mime)], (_, _) => Task.FromResult(1)));
        Assert.Empty(Files());
    }
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(5242881)]
    public async Task RejectsEmptyTruncatedAndOversized(int length)
    {
        await Assert.ThrowsAsync<UtilityPhotoValidationException>(() => service.SaveAndPersistAsync([Photo(data: new byte[length])], (_, _) => Task.FromResult(1)));
        Assert.Empty(Files());
    }
    [Fact]
    public async Task ThreePhotosHaveSafePrivateNamesAndCanBeRead()
    {
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var urls = await service.SaveAndPersistAsync([Photo("../../evil.jpeg"), Photo(), Photo("x.png", "image/png", png)], (paths, _) => Task.FromResult(paths.ToArray()));
        Assert.Equal(3, urls.Distinct().Count());
        Assert.All(urls, url => Assert.True(LocalPhotoStorage.IsPrivateUrl(url)));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "wwwroot"), "*", SearchOption.AllDirectories));
        var photo = await storage.OpenPrivateAsync(urls[2]);
        Assert.NotNull(photo); await using var stream = photo.Content;
        Assert.Equal("image/png", photo.ContentType);
        using var copy = new MemoryStream(); await stream.CopyToAsync(copy); Assert.Equal(png, copy.ToArray());
    }
    [Fact]
    public async Task ExactLimitAccepted()
    {
        var bytes = new byte[UtilityPhotoUploadService.MaxPhotoBytes]; bytes[0] = 255; bytes[1] = 216; bytes[2] = 255;
        await service.SaveAndPersistAsync([Photo(data: bytes)], (_, _) => Task.FromResult(1)); Assert.Single(Files());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PersistenceFailureOrCancellationRemovesAllFiles(bool cancelled)
    {
        await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAndPersistAsync<int>([Photo(), Photo()], (_, _) => throw (cancelled ? new OperationCanceledException() : new InvalidOperationException("database failed"))));
        Assert.Empty(Files());
    }
    [Fact]
    public async Task FailedSecondWriteRemovesFirst()
    {
        var fake = new Mock<IPhotoStorage>();
        fake.SetupSequence(s => s.SavePrivateAsync(It.IsAny<Stream>(), ".jpg", It.IsAny<CancellationToken>())).ReturnsAsync("first").ThrowsAsync(new IOException("disk full"));
        var uploads = new UtilityPhotoUploadService(fake.Object, NullLogger<UtilityPhotoUploadService>.Instance);
        await Assert.ThrowsAsync<IOException>(() => uploads.SaveAndPersistAsync([Photo(), Photo()], (_, _) => Task.FromResult(1)));
        fake.Verify(s => s.DeletePrivateAsync("first", CancellationToken.None), Times.Once);
    }
    [Theory]
    [InlineData(8, 9)]
    [InlineData(8, 5242881)]
    public async Task ActualStreamLengthCannotBypassDeclaredLength(long declared, int actual)
    {
        var fake = new Mock<IFormFile>();
        fake.SetupGet(f => f.Length).Returns(declared);
        fake.SetupGet(f => f.FileName).Returns("a.jpg");
        fake.SetupGet(f => f.ContentType).Returns("image/jpeg");
        fake.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[actual]));
        await Assert.ThrowsAsync<UtilityPhotoValidationException>(() => service.SaveAndPersistAsync([fake.Object], (_, _) => Task.FromResult(1)));
        Assert.Empty(Files());
    }
    [Fact]
    public async Task DisguisedExecutableRejected()
    {
        await Assert.ThrowsAsync<UtilityPhotoValidationException>(() => service.SaveAndPersistAsync([Photo(data: [77, 90, 0, 0, 0, 0, 0, 0])], (_, _) => Task.FromResult(1)));
        Assert.Empty(Files());
    }
    [Theory]
    [InlineData("/api/report-photos/../../secret.jpg")]
    [InlineData("/uploads/reports/abc.jpg")]
    [InlineData("/api/report-photos/00000000000000000000000000000000.exe")]
    public async Task UnsafePathsCannotBeOpenedOrDeleted(string url)
    {
        Assert.False(LocalPhotoStorage.IsPrivateUrl(url)); Assert.Null(await storage.OpenPrivateAsync(url)); await storage.DeletePrivateAsync(url);
    }
    [Theory]
    [InlineData("Resident", 1, true)]
    [InlineData("Resident", 2, false)]
    [InlineData("Crew", 2, true)]
    [InlineData("Crew", 1, false)]
    [InlineData("Official", 2, true)]
    [InlineData("CityOfficial", 2, true)]
    [InlineData("Dispatcher", 2, true)]
    public async Task ReadRequiresReportOwnershipOrStaffAccess(string role, int userId, bool allowed)
    {
        var (db, connection) = TestDb.Create();
        await using var dbScope = db; await using var connectionScope = connection;
        TestDb.AddUser(db, 1); TestDb.AddUser(db, 2);
        var urls = await service.SaveAndPersistAsync([Photo()], (paths, _) => Task.FromResult(paths.ToArray()));
        var crew = new Nagorik.Api.Models.Crew { Name = "Crew", UserId = 2 };
        db.Crews.Add(crew);
        db.Reports.Add(new Nagorik.Api.Models.Report { UserId = 1, Title = "Photo report", PhotoPaths = urls.ToList(), AssignedCrew = crew });
        await db.SaveChangesAsync();
        var controller = new Nagorik.Api.Controllers.ReportPhotosController(db, storage);
        var identity = new System.Security.Claims.ClaimsIdentity(new[] {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role) }, "test");
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) } };
        var result = await controller.Get(urls[0][LocalPhotoStorage.PrivateUrlPrefix.Length..], default);
        if (allowed)
        {
            var file = Assert.IsType<Microsoft.AspNetCore.Mvc.FileStreamResult>(result);
            await file.FileStream.DisposeAsync(); Assert.Equal("image/jpeg", file.ContentType);
            Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"]);
        }
        else Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await controller.Get(Guid.NewGuid().ToString("N") + ".jpg", default));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
