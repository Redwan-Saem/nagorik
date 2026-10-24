using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Nagorik.Api.Services;

// Legacy report photos stay on disk and retain their URLs, but must be served
// by ReportPhotosController after authorization, never by static middleware.
public sealed class ReportPhotoStaticFiles(IFileProvider inner, string webRoot) : IFileProvider
{
    private readonly string protectedRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "reports"))
        .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    public IFileInfo GetFileInfo(string subpath)
    {
        var file = inner.GetFileInfo(subpath);
        if (file.PhysicalPath is { } physical &&
            Path.GetFullPath(physical).StartsWith(protectedRoot, StringComparison.OrdinalIgnoreCase))
            return new NotFoundFileInfo(subpath);
        return file;
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
    public IChangeToken Watch(string filter) => inner.Watch(filter);
}
