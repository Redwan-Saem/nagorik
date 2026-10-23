using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

public sealed partial class LoadsheddingApiController
{
    [HttpPost("{id:int}/publish"), ValidateAntiForgeryToken, RequestSizeLimit(16384)]
    public Task<IActionResult> Publish(int id, PublishScheduleRequest input, [FromServices] LoadsheddingScheduleService service, CancellationToken ct) =>
        Run(async () => ScheduleResponse(await service.PublishAsync(id, input, Actor(), ct), service));

    [HttpGet("{id:int}/audit")]
    public async Task<IActionResult> Audit(int id, [FromServices] AppDbContext context, CancellationToken ct)
    {
        if (!await context.LoadsheddingSchedules.AnyAsync(s => s.Id == id, ct)) return NotFound();
        return Ok(await context.ScheduleAudits.AsNoTracking().Where(a => a.ScheduleId == id).OrderByDescending(a => a.Id)
            .Take(200).Select(a => new { a.Id, a.Action, a.ActorUserId, a.AtUtc, a.Details }).ToListAsync(ct));
    }
}
