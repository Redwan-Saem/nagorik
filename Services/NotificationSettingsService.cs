using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services
{
    public record NotificationSettingsDto(
        bool Enabled,
        List<int> SelectedZoneIds,
        List<ZoneOption> Zones
    );

    public record ZoneOption(
        int Id,
        string Name
    );


    public class NotificationSettingsService
    {
        private readonly AppDbContext _db;


        public NotificationSettingsService(AppDbContext db)
        {
            _db = db;
        }


        public async Task<NotificationSettingsDto> GetAsync(string userId)
        {
            var pref = await _db.NotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);


            var selected = await _db.AreaSubscriptions
                .Where(a => a.UserId == userId)
                .Select(a => int.Parse(a.ZoneId))
                .ToListAsync();


            var zones = await _db.ZoneMapData
                .OrderBy(z => z.ZoneId)
                .Select(z => new ZoneOption(
                    z.Id,
                    z.ZoneId
                ))
                .ToListAsync();


            return new NotificationSettingsDto(
                pref?.NotificationsEnabled ?? false,
                selected,
                zones
            );
        }



        public async Task<(bool ok, string? error)> SaveAsync(
            string userId,
            bool enabled,
            List<int> zoneIds)
        {
            zoneIds = zoneIds
                .Distinct()
                .ToList();


            var valid = await _db.ZoneMapData
                .CountAsync(z => zoneIds.Contains(z.Id));


            if (valid != zoneIds.Count)
            {
                return (
                    false,
                    "One or more selected areas do not exist."
                );
            }



            var pref = await _db.NotificationPreferences
                .FindAsync(userId);


            if (pref == null)
            {
                _db.NotificationPreferences.Add(
                    new NotificationPreference
                    {
                        UserId = userId,
                        NotificationsEnabled = enabled
                    });
            }
            else
            {
                pref.NotificationsEnabled = enabled;
            }



            _db.AreaSubscriptions.RemoveRange(
                _db.AreaSubscriptions
                    .Where(a => a.UserId == userId)
            );



            _db.AreaSubscriptions.AddRange(
                zoneIds.Select(z =>
                    new AreaSubscription
                    {
                        UserId = userId,
                        ZoneId = z.ToString()
                    })
            );



            await _db.SaveChangesAsync();


            return (true, null);
        }




        public async Task SavePushSubscriptionAsync(
            string userId,
            string endpoint,
            string p256dh,
            string auth)
        {
            var existing =
                await _db.UserPushSubscriptions
                .FirstOrDefaultAsync(
                    s => s.Endpoint == endpoint);



            if (existing == null)
            {
                _db.UserPushSubscriptions.Add(
                    new UserPushSubscription
                    {
                        UserId = userId,
                        Endpoint = endpoint,
                        P256dh = p256dh,
                        Auth = auth
                    });
            }
            else
            {
                existing.UserId = userId;
                existing.P256dh = p256dh;
                existing.Auth = auth;
            }



            await _db.SaveChangesAsync();
        }





        public async Task RemovePushSubscriptionAsync(
            string userId,
            string endpoint)
        {
            var subscription =
                await _db.UserPushSubscriptions
                .FirstOrDefaultAsync(
                    x => x.Endpoint == endpoint &&
                         x.UserId == userId);



            if (subscription != null)
            {
                _db.UserPushSubscriptions.Remove(subscription);

                await _db.SaveChangesAsync();
            }
        }
    }
}