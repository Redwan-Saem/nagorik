using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record UtilityAnomalyCreated(int Id, string ReferenceNo, string Category, string Status,
    string Urgency, string Utility, string RoutingOutcome, IReadOnlyList<string> PhotoPaths,
    bool IsPossibleDuplicate = false, int? DuplicateOfReportId = null, string? Warning = null);
public sealed record UtilityAnomalySubmissionResult(UtilityAnomalyCreated? Report, Dictionary<string, string[]> Errors,
    bool InvalidIdentity = false, int? RetryAfterSeconds = null);

public sealed class UtilityAnomalySubmissionService(AppDbContext db, UtilityRoutingService routing, UtilityPhotoUploadService photos, TimeProvider clock)
{
    private sealed class SubmissionLimitException(int retryAfterSeconds) : Exception
    {
        public int RetryAfterSeconds { get; } = retryAfterSeconds;
    }

    // Area plus case/Unicode/whitespace-normalized address defines a duplicate location.
    public static string NormalizeLocation(string location) => string.Join(" ",
        location.Normalize(System.Text.NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public async Task<UtilityAnomalySubmissionResult> CreateAsync(int userId, CreateUtilityAnomalyRequest request, CancellationToken ct = default)
    {
        if (userId <= 0 || !await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == "Resident", ct))
            return new(null, [], true);
        var validation = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validation, validateAllProperties: true);
        var errors = validation.SelectMany(v => v.MemberNames.DefaultIfEmpty("Request").Select(member => (member, v.ErrorMessage!)))
            .GroupBy(v => v.member).ToDictionary(g => g.Key, g => g.Select(v => v.Item2).ToArray());
        if (errors.Count != 0) return new(null, errors);
        var route = await routing.ResolveAsync(request.AnomalyType!.Value, request.AreaId, request.Latitude, request.Longitude, ct);
        if (route.Outcome == UtilityRoutingOutcome.InvalidInput)
            return new(null, new() { [nameof(request.AreaId)] = [route.Message] });

        Report? report = null;
        ReportStatusHistory? history = null;
        try
        {
            var created = await photos.SaveAndPersistAsync(request.Photos, async (paths, token) =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(token);
                // SQLite starts this write transaction in immediate mode: concurrent submitters
                // cannot both check the same quota and then insert beyond it.
                var now = clock.GetUtcNow().UtcDateTime;
                var hourStart = now.AddHours(-1);
                var recent = await db.Reports.AsNoTracking()
                    .Where(r => r.UserId == userId && r.Category == ReportCategory.UtilityAnomaly && r.CreatedAt > hourStart && r.CreatedAt <= now)
                    .OrderByDescending(r => r.CreatedAt).Select(r => r.CreatedAt).Take(10).ToListAsync(token);
                if (recent.Count == 10)
                    throw new SubmissionLimitException(Math.Max(1, (int)Math.Ceiling((recent[^1].AddHours(1) - now).TotalSeconds)));
                var duplicateStart = now.AddHours(-24);
                var candidates = await db.Reports.AsNoTracking()
                    .Where(r => r.UserId == userId && r.Category == ReportCategory.UtilityAnomaly &&
                        r.AnomalyType == request.AnomalyType && r.AreaId == request.AreaId &&
                        r.CreatedAt > duplicateStart && r.CreatedAt <= now)
                    .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
                    .Select(r => new { r.Id, r.Location }).ToListAsync(token);
                var location = NormalizeLocation(request.AddressText);
                var duplicate = candidates.FirstOrDefault(r => NormalizeLocation(r.Location) == location);
                report = new Report
                {
                    UserId = userId, Title = "Utility anomaly: " + request.AnomalyType.Value,
                    Description = request.Description.Trim(), Location = request.AddressText.Trim(), AreaId = request.AreaId,
                    Latitude = request.Latitude ?? 0, Longitude = request.Longitude ?? 0,
                    Category = ReportCategory.UtilityAnomaly, AnomalyType = request.AnomalyType, Utility = route.Utility,
                    Authority = route.Utility is UtilityProvider.Desco or UtilityProvider.Dpdc ? Authority.DpdcDesco : Authority.Other,
                    Status = ReportStatus.Pending, Urgency = UtilityAnomalyRules.GetUrgency(request.AnomalyType.Value),
                    PhotoPaths = paths.ToList(), ImageUrl = paths[0], CreatedAt = now,
                    IsPossibleDuplicate = duplicate != null, DuplicateOfReportId = duplicate?.Id
                };
                db.Reports.Add(report);
                await db.SaveChangesAsync(token);
                // SQLite's AUTOINCREMENT report key is allocated by the database, never MAX()+1.
                report.ReferenceNo = $"UA-{report.Id:D6}";
                history = new ReportStatusHistory { ReportId = report.Id, Status = ReportStatus.Pending,
                    ChangedByUserId = userId, ChangedAtUtc = report.CreatedAt };
                db.ReportStatusHistories.Add(history);
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                return new UtilityAnomalyCreated(report.Id, report.ReferenceNo, report.Category.ToString(), report.Status.ToString(),
                    report.Urgency.ToString(), route.Utility.ToString(), route.Outcome.ToString(), paths,
                    report.IsPossibleDuplicate, report.DuplicateOfReportId,
                    duplicate != null ? "A similar report was submitted at this location in the past 24 hours. Your report has still been submitted." : null);
            }, ct);
            return new(created, []);
        }
        catch (SubmissionLimitException error)
        {
            return new(null, [], RetryAfterSeconds: error.RetryAfterSeconds);
        }
        catch (UtilityPhotoValidationException error)
        {
            return new(null, new() { [nameof(request.Photos)] = [error.Message] });
        }
        catch
        {
            if (history != null) db.Entry(history).State = EntityState.Detached;
            if (report != null) db.Entry(report).State = EntityState.Detached;
            throw;
        }
    }
}
