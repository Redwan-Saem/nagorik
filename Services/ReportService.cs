using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public record ReportResult(
    bool Success,
    int ReportId,
    Dictionary<string, string> Errors)
{
    public static ReportResult Ok(int id) =>
        new(true, id, new());

    public static ReportResult Fail(Dictionary<string, string> errors) =>
        new(false, 0, errors);
}

public interface IReportService
{
    Task<ReportResult> CreateAsync(
        string userId,
        CreateReportRequest request);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly IPhotoStorage _storage;

    public ReportService(
        AppDbContext db,
        IPhotoStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<ReportResult> CreateAsync(
        string userId,
        CreateReportRequest request)
    {
        var errors = new Dictionary<string, string>();

        if (!ReportValidator.IsInDhaka(
                request.Latitude!.Value,
                request.Longitude!.Value))
        {
            errors["Location"] =
                "The location is outside the Dhaka service area.";
        }

        var header = new byte[8];

        await using (var stream = request.Photo!.OpenReadStream())
        {
            int totalRead = 0;

            while (totalRead < header.Length)
            {
                int read = await stream.ReadAsync(
                    header.AsMemory(
                        totalRead,
                        header.Length - totalRead));

                if (read == 0)
                    break;

                totalRead += read;
            }
        }

        var photoError = ReportValidator.ValidatePhoto(
            request.Photo.FileName,
            request.Photo.Length,
            header);

        if (photoError != null)
            errors["Photo"] = photoError;

        if (errors.Count > 0)
            return ReportResult.Fail(errors);


        string imageUrl;

        await using (var stream = request.Photo.OpenReadStream())
        {
            imageUrl = await _storage.SaveAsync(
                stream,
                Path.GetExtension(request.Photo.FileName)
                    .ToLowerInvariant());
        }


        if (!int.TryParse(userId, out var userIdValue))
        {
            return ReportResult.Fail(new Dictionary<string, string>
            {
                ["User"] = "Invalid user ID."
            });
        }


        var nearby = await DuplicateDetector.FindNearbyOpenAsync(
            _db,
            request.Category!.Value,
            request.Latitude.Value,
            request.Longitude.Value
        );


        var report = new Report
        {
            Title = request.Category.Value.ToString(),

            Description = request.Description.Trim(),

            Category = request.Category.Value,

            Authority = ReportRouting.GetAuthority(
                request.Category.Value),

            Status = ReportStatus.Pending,

            Location = request.AddressText.Trim(),

            ImageUrl = imageUrl,

            UserId = userIdValue,

            Latitude = request.Latitude.Value,

            Longitude = request.Longitude.Value,

            IsPossibleDuplicate = nearby != null,

            DuplicateOfReportId = nearby?.Id,

            CreatedAt = DateTime.UtcNow
        };


        _db.Reports.Add(report);

        await _db.SaveChangesAsync();

        return ReportResult.Ok(report.Id);
    }
}