using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record SosAlertDto(int Id, double? Latitude, double? Longitude, string AddressText,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string Status, int? ResponderUserId);

public enum SosOutcome { Created, Success, NotFound, Conflict, Invalid, UserNotFound }
public sealed record SosResult(SosOutcome Outcome, SosAlertDto? Alert = null,
    Dictionary<string, string[]>? Errors = null);

public class SosAlertService(AppDbContext db, SosResponderNotifier responderNotifier)
{
    public async Task<SosAlertDto?> GetActiveAsync(int userId, CancellationToken ct = default) =>
        await db.SosAlerts.AsNoTracking()
            .Where(a => a.UserId == userId && (a.Status == SosAlertStatus.Active || a.Status == SosAlertStatus.Responding))
            .Select(a => ToDto(a)).SingleOrDefaultAsync(ct);

    public async Task<SosAlertDto?> GetByIdAsync(int id, int userId, CancellationToken ct = default)
    {
        var alert = await db.SosAlerts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        return alert == null ? null : ToDto(alert);
    }


    public async Task<SosResult> TriggerAsync(int userId, SosLocationRequest request, CancellationToken ct = default)
    {
        var invalid = Validate(request);
        if (invalid != null) return invalid;
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct)) return new(SosOutcome.UserNotFound);
        var existing = await GetActiveAsync(userId, ct);
        if (existing != null) return new(SosOutcome.Conflict, existing);

        var now = DateTime.UtcNow;
        var alert = new SosAlert
        {
            UserId = userId,
            Latitude = request.Latitude, Longitude = request.Longitude,
            AddressText = request.AddressText?.Trim() ?? "", CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var history = Snapshot(alert, SosAlertEventType.Triggered, userId);
        db.SosAlerts.Add(alert);
        db.SosAlertEvents.Add(history);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            // Another trigger may have passed the preflight at the same time.
            db.Entry(history).State = EntityState.Detached;
            db.Entry(alert).State = EntityState.Detached;
            existing = await GetActiveAsync(userId, ct);
            if (existing == null) throw;
            return new(SosOutcome.Conflict, existing);
        }
        await responderNotifier.NotifyCreatedAsync(alert);
        return new(SosOutcome.Created, ToDto(alert));
    }


    public async Task<SosResult> UpdateLocationAsync(int id, int userId, SosLocationRequest request, CancellationToken ct = default)
    {
        var invalid = Validate(request);
        if (invalid != null) return invalid;
        var alert = await db.SosAlerts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (alert == null) return new(SosOutcome.NotFound);
        if (!IsActive(alert)) return new(SosOutcome.Conflict);
        var address = request.AddressText?.Trim() ?? "";
        if (alert.Latitude == request.Latitude && alert.Longitude == request.Longitude && alert.AddressText == address)
            return new(SosOutcome.Success, ToDto(alert));
        alert.Latitude = request.Latitude;
        alert.Longitude = request.Longitude;
        alert.AddressText = address;
        AdvanceTimestamp(alert);
        return await SaveEventAsync(alert, SosAlertEventType.LocationUpdated, userId, ct);
    }

    public async Task<SosResult> CancelAsync(int id, int userId, CancellationToken ct = default)
    {
        var alert = await db.SosAlerts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (alert == null) return new(SosOutcome.NotFound);
        if (alert.Status == SosAlertStatus.Cancelled) return new(SosOutcome.Success, ToDto(alert));
        if (!IsActive(alert)) return new(SosOutcome.Conflict);
        alert.Status = SosAlertStatus.Cancelled;
        AdvanceTimestamp(alert);
        return await SaveEventAsync(alert, SosAlertEventType.Cancelled, userId, ct);
    }

    private async Task<SosResult> SaveEventAsync(SosAlert alert, SosAlertEventType eventType, int userId, CancellationToken ct)
    {
        var history = Snapshot(alert, eventType, userId);
        db.SosAlertEvents.Add(history);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(history).State = EntityState.Detached;
            await db.Entry(alert).ReloadAsync(ct);
            return new(SosOutcome.Conflict);
        }
        return new(SosOutcome.Success, ToDto(alert));
    }

    private static SosAlertEvent Snapshot(SosAlert alert, SosAlertEventType type, int actor) => new()
    {
        SosAlert = alert, EventType = type, Status = alert.Status, ActorUserId = actor,
        Latitude = alert.Latitude, Longitude = alert.Longitude, AddressText = alert.AddressText,
        OccurredAtUtc = alert.UpdatedAtUtc
    };

    private static bool IsActive(SosAlert alert) => alert.Status is SosAlertStatus.Active or SosAlertStatus.Responding;
    private static void AdvanceTimestamp(SosAlert alert)
    {
        var now = DateTime.UtcNow;
        alert.UpdatedAtUtc = now > alert.UpdatedAtUtc ? now : alert.UpdatedAtUtc.AddTicks(1);
    }

    private static SosResult? Validate(SosLocationRequest request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, true)) return null;
        var errors = results.SelectMany(r => r.MemberNames.DefaultIfEmpty("").Select(m => (Member: m, Error: r.ErrorMessage!)))
            .GroupBy(e => e.Member).ToDictionary(g => g.Key, g => g.Select(e => e.Error).ToArray());
        return new(SosOutcome.Invalid, Errors: errors);
    }

    private static SosAlertDto ToDto(SosAlert a) => new(a.Id, a.Latitude, a.Longitude, a.AddressText,
        a.CreatedAtUtc, a.UpdatedAtUtc, a.Status.ToString(), a.ResponderUserId);
}
