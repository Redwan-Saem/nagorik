namespace Nagorik.Api.Services;

public sealed class UtilityPhotoValidationException(string message) : Exception(message);

// The callback must commit its database transaction before returning successfully.
public sealed class UtilityPhotoUploadService(IPhotoStorage storage, ILogger<UtilityPhotoUploadService> logger)
{
    public const int MaxPhotoBytes = 5 * 1024 * 1024;

    public async Task<T> SaveAndPersistAsync<T>(IReadOnlyList<IFormFile> photos,
        Func<IReadOnlyList<string>, CancellationToken, Task<T>> persistAsync, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(persistAsync);
        if (photos is null || photos.Count is < 1 or > 3)
            throw new UtilityPhotoValidationException("Provide one to three photos.");
        var validated = new List<(byte[] Data, string Extension)>();
        foreach (var photo in photos)
        {
            if (photo is null || photo.Length is <= 0 or > MaxPhotoBytes)
                throw new UtilityPhotoValidationException("Each photo must contain data and be at most 5 MiB.");
            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            var jpeg = extension is ".jpg" or ".jpeg";
            if ((!jpeg && extension != ".png") || photo.ContentType != (jpeg ? "image/jpeg" : "image/png"))
                throw new UtilityPhotoValidationException("Photos must have matching JPG/PNG extensions and MIME types.");
            await using var input = photo.OpenReadStream();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, MaxPhotoBytes + 1L - buffer.Length)), ct)) > 0)
            {
                buffer.Write(chunk, 0, count);
                if (buffer.Length > MaxPhotoBytes) throw new UtilityPhotoValidationException("Photo exceeds 5 MiB.");
            }
            var bytes = buffer.ToArray();
            if (bytes.LongLength != photo.Length || (jpeg
                ? bytes.Length < 3 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[2] != 0xff
                : bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })))
                throw new UtilityPhotoValidationException("Photo content does not match its declared image format or length.");
            validated.Add((bytes, jpeg ? ".jpg" : ".png"));
        }

        var saved = new List<string>();
        try
        {
            foreach (var photo in validated)
            {
                await using var stream = new MemoryStream(photo.Data, writable: false);
                saved.Add(await storage.SavePrivateAsync(stream, photo.Extension, ct));
            }
            return await persistAsync(saved.AsReadOnly(), ct);
        }
        catch
        {
            foreach (var url in saved)
            {
                try { await storage.DeletePrivateAsync(url, CancellationToken.None); }
                catch (Exception cleanupError) { logger.LogError(cleanupError, "Failed to remove uncommitted report photo {PhotoUrl}", url); }
            }
            throw;
        }
    }
}
