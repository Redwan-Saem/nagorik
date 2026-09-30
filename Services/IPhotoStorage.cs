using Microsoft.AspNetCore.Hosting;

namespace Nagorik.Api.Services;

public interface IPhotoStorage
{
    Task<string> SaveAsync(Stream data, string extension);
}

public class LocalPhotoStorage : IPhotoStorage
{
    private readonly string _root;

    public LocalPhotoStorage(IWebHostEnvironment env)
    {
        _root = Path.Combine(
            env.WebRootPath,
            "uploads",
            "reports");

        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream data, string extension)
    {
        var name = $"{Guid.NewGuid():N}{extension}";

        await using var fs = File.Create(
            Path.Combine(_root, name));

        await data.CopyToAsync(fs);

        return $"/uploads/reports/{name}";
    }
}