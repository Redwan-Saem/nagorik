using System.IO;

namespace Nagorik.Api.Services;

public static class ReportValidator
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    public static bool IsInDhaka(double lat, double lng) =>
        lat is >= 23.65 and <= 23.95 &&
        lng is >= 90.30 and <= 90.55;

    public static string? ValidatePhoto(
        string fileName,
        long length,
        byte[] header)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext is not (".jpg" or ".jpeg" or ".png"))
            return "Only JPG and PNG photos are allowed.";

        if (length == 0)
            return "The photo is empty.";

        if (length > MaxPhotoBytes)
            return "The photo must be 5 MB or smaller.";

        bool jpg =
            header.Length >= 3 &&
            header[0] == 0xFF &&
            header[1] == 0xD8 &&
            header[2] == 0xFF;

        bool png =
            header.Length >= 4 &&
            header[0] == 0x89 &&
            header[1] == 0x50 &&
            header[2] == 0x4E &&
            header[3] == 0x47;

        return (jpg || png)
            ? null
            : "The file is not a valid JPG or PNG image.";
    }
}