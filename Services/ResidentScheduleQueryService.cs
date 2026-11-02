using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed record ResidentOutage(DateOnly Date, int StartMinute, int EndMinute, DateTime StartAtUtc, DateTime EndAtUtc, bool IsOngoing);
public sealed record ResidentScheduleDay(DateOnly Date, bool IsToday, bool IsPublished, DateTime? PublishedAtUtc, IReadOnlyList<ResidentOutage> Outages);
public sealed record ResidentScheduleResult(int ZoneId, string ZoneCode, string ZoneName, string TimeZone,
    DateOnly Today, DateTime FetchedAtUtc, DateTime? LastUpdatedAtUtc, string Status, ResidentOutage? NextOutage, IReadOnlyList<ResidentScheduleDay> Days);

public sealed class ResidentScheduleQueryService(AppDbContext db, TimeProvider clock)
{
    public async Task<ResidentScheduleResult?> GetAsync(int zoneId, CancellationToken ct = default)
    {
        var zone = await db.Zones.AsNoTracking().Where(z => z.Id == zoneId).Select(z => new { z.Id, z.Code, z.Name }).SingleOrDefaultAsync(ct);
        if (zone == null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(DhakaTime.ToLocal(now));var end = today.AddDays(6);
        // The (ZoneId, Date) primary key bounds this query to seven publication
        // pointers. A Draft/Superseded row is excluded even if a pointer is corrupt.
        var publications = await db.SchedulePublications.AsNoTracking().Where(p => p.ZoneId == zoneId && p.Date >= today && p.Date <= end &&
            p.Schedule.State == ScheduleState.Published && p.Schedule.PublishedAtUtc != null)
            .Select(p => new { p.Date, p.ScheduleId, p.Schedule.PublishedAtUtc }).ToListAsync(ct);
        var ids = publications.Select(p => p.ScheduleId).Distinct().ToArray();
        var slots = await db.LoadsheddingSlots.AsNoTracking().Where(s => s.ZoneId == zoneId && ids.Contains(s.ScheduleId) && s.Date >= today && s.Date <= end)
            .OrderBy(s => s.Date).ThenBy(s => s.StartMinute)
            .Select(s => new { s.ScheduleId, s.Date, s.StartMinute, s.EndMinute }).ToListAsync(ct);
        var days = new List<ResidentScheduleDay>();
        for (var day = today; day <= end; day = day.AddDays(1))
        {
            var publication = publications.SingleOrDefault(p => p.Date == day);
            var outages = slots.Where(s => s.Date == day && s.ScheduleId == publication?.ScheduleId).Select(s =>
            {
                var start = DhakaTime.ToUtc(s.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(s.StartMinute));
                var finish = DhakaTime.ToUtc(s.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(s.EndMinute));
                return new ResidentOutage(s.Date, s.StartMinute, s.EndMinute, start, finish, start <= now && finish > now);
            }).ToArray();
            days.Add(new(day, day == today, publication != null, publication?.PublishedAtUtc is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null, outages));
        }
        var next = days.SelectMany(d => d.Outages).Where(s => s.EndAtUtc > now).OrderBy(s => s.StartAtUtc).FirstOrDefault();
        var published = days.Count(d => d.IsPublished);
        var status = published == 0 ? "NotPublished" : published < 7 ? "PartiallyPublished" : days.All(d => d.Outages.Count == 0) ? "NoOutages" : "Published";
        return new(zone.Id, zone.Code, zone.Name, "Asia/Dhaka", today, now,
            days.Where(d => d.PublishedAtUtc != null).Select(d => d.PublishedAtUtc).Max(), status, next, days);
    }
}
