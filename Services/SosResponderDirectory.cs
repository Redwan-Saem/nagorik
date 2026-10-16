using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public class SosResponderDirectory(AppDbContext db, IOptions<SosOptions> options)
{
    public async Task<List<int>> GetUserIdsAsync(CancellationToken ct = default)
    {
        var emails = options.Value.ResponderEmails.Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant()).Distinct().ToArray();
        if (emails.Length == 0) return [];
        return await db.Users.AsNoTracking().Where(u => emails.Contains(u.Email.ToLower()))
            .Select(u => u.Id).ToListAsync(ct);
    }

    public async Task<bool> IsResponderAsync(int userId, CancellationToken ct = default) =>
        (await GetUserIdsAsync(ct)).Contains(userId);
}
