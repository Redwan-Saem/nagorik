using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record UtilityAnomalyCreated(int Id, string ReferenceNo, string Category, string Status,
    string Urgency, string Utility, string RoutingOutcome, IReadOnlyList<string> PhotoPaths);
public sealed record UtilityAnomalySubmissionResult(UtilityAnomalyCreated? Report, Dictionary<string, string[]> Errors, bool InvalidIdentity = false);

public sealed class UtilityAnomalySubmissionService(AppDbContext db, UtilityRoutingService routing, UtilityPhotoUploadService photos)
{
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
                report = new Report
                {
                    UserId = userId, Title = "Utility anomaly: " + request.AnomalyType.Value,
                    Description = request.Description.Trim(), Location = request.AddressText.Trim(), AreaId = request.AreaId,
                    Latitude = request.Latitude ?? 0, Longitude = request.Longitude ?? 0,
                    Category = ReportCategory.UtilityAnomaly, AnomalyType = request.AnomalyType, Utility = route.Utility,
                    Authority = route.Utility is UtilityProvider.Desco or UtilityProvider.Dpdc ? Authority.DpdcDesco : Authority.Other,
                    Status = ReportStatus.Pending, Urgency = UtilityAnomalyRules.GetUrgency(request.AnomalyType.Value),
                    PhotoPaths = paths.ToList(), ImageUrl = paths[0], CreatedAt = DateTime.UtcNow
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
                    report.Urgency.ToString(), route.Utility.ToString(), route.Outcome.ToString(), paths);
            }, ct);
            return new(created, []);
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
