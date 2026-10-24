using Microsoft.AspNetCore.Hosting;

namespace Nagorik.Api.Services;

public interface IPhotoStorage
{
    Task<string> SaveAsync(Stream data, string extension);
    Task<string> SavePrivateAsync(Stream data, string extension, CancellationToken cancellationToken = default);
    Task<StoredReportPhoto?> OpenPrivateAsync(string url, CancellationToken cancellationToken = default);
    Task DeletePrivateAsync(string url, CancellationToken cancellationToken = default);
}

public sealed record StoredReportPhoto(Stream Content, string ContentType);

public class LocalPhotoStorage : IPhotoStorage
{
    private readonly string _root;
    private readonly string _privateRoot;
    public const string PrivateUrlPrefix = "/api/report-photos/";

    public LocalPhotoStorage(IWebHostEnvironment env)
    {
        _root = Path.Combine(
            env.WebRootPath,
            "uploads",
            "reports");

        Directory.CreateDirectory(_root);
        _privateRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "report-photos"));
        var webRoot = Path.GetFullPath(env.WebRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((_privateRoot + Path.DirectorySeparatorChar).StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Private photo storage must be outside the web root.");
    }

    public Task<string> SaveAsync(Stream data, string extension) => SavePrivateAsync(data, extension);

    public const string LegacyUrlPrefix = "/uploads/reports/";
    public static bool IsLegacyUrl(string url)
    {
        if (!url.StartsWith(LegacyUrlPrefix, StringComparison.Ordinal)) return false;
        var name = url[LegacyUrlPrefix.Length..];
        var extension = Path.GetExtension(name);
        return (extension is ".jpg" or ".jpeg" or ".png") && name.Length == 32 + extension.Length
            && Guid.TryParseExact(name[..32], "N", out _);
    }

    public static bool IsPrivateUrl(string url)
    {
        if (!url.StartsWith(PrivateUrlPrefix, StringComparison.Ordinal)) return false;
        var name = url[PrivateUrlPrefix.Length..];
        return name.Length == 36 && (name.EndsWith(".jpg", StringComparison.Ordinal) || name.EndsWith(".png", StringComparison.Ordinal))
            && Guid.TryParseExact(name[..32], "N", out _);
    }

    public async Task<string> SavePrivateAsync(Stream data, string extension, CancellationToken cancellationToken = default)
    {
        extension = extension.ToLowerInvariant();
        if (extension == ".jpeg") extension = ".jpg";
        if (extension is not (".jpg" or ".png")) throw new ArgumentException("Unsupported photo extension.", nameof(extension));
        Directory.CreateDirectory(_privateRoot);
        var name = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_privateRoot, name);
        var created = false;
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            created = true;
            await data.CopyToAsync(file, cancellationToken);
            return PrivateUrlPrefix + name;
        }
        catch
        {
            if (created) File.Delete(path);
            throw;
        }
    }

    public Task<StoredReportPhoto?> OpenPrivateAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isPrivate = IsPrivateUrl(url);
        if (!isPrivate && !IsLegacyUrl(url)) return Task.FromResult<StoredReportPhoto?>(null);
        try
        {
            var path = isPrivate ? Path.Combine(_privateRoot, url[PrivateUrlPrefix.Length..]) : Path.Combine(_root, url[LegacyUrlPrefix.Length..]);
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            return Task.FromResult<StoredReportPhoto?>(new(stream, url.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg"));
        }
        catch (FileNotFoundException) { return Task.FromResult<StoredReportPhoto?>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<StoredReportPhoto?>(null); }
    }

    public Task DeletePrivateAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsPrivateUrl(url)) File.Delete(Path.Combine(_privateRoot, url[PrivateUrlPrefix.Length..]));
        return Task.CompletedTask;
    }
}
