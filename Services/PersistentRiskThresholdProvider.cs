using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nagorik.Api.Models;

namespace Nagorik.Api.Services;

public sealed class PersistentRiskThresholdProvider(AppDbContext db, IOptions<RiskScoringOptions> defaults)
    : RiskThresholdProvider(defaults)
{
    public override async Task<RiskScoringOptions> GetAsync(CancellationToken ct = default) =>
        (await db.RiskThresholdSettings.AsNoTracking().SingleOrDefaultAsync(ct))?.ToOptions() ?? await base.GetAsync(ct);
}
