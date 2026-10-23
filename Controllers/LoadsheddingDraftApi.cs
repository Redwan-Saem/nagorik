using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

public sealed partial class LoadsheddingApiController
{
    [HttpGet("")]
    public async Task<IActionResult> List([FromServices] AppDbContext context, CancellationToken ct) => Ok(await context.LoadsheddingSchedules
        .AsNoTracking().OrderByDescending(s => s.Id).Take(100).Select(s => new { s.Id, s.StartDate, s.EndDate,
            state = s.State.ToString(), s.Revision, s.CreatedAtUtc, s.PublishedAtUtc }).ToListAsync(ct));

    [HttpGet("{id:int}")]
    public Task<IActionResult> Detail(int id, [FromServices] LoadsheddingScheduleService service, CancellationToken ct) =>
        Run(async () => ScheduleResponse(await service.GetAsync(id, ct), service));

    [HttpPost(""), ValidateAntiForgeryToken, RequestSizeLimit(65536)]
    public Task<IActionResult> Generate(GenerateScheduleRequest input, [FromServices] LoadsheddingScheduleService service, CancellationToken ct) =>
        Run(async () => ScheduleResponse(await service.GenerateAsync(input.ToInput(), Actor(), ct), service));

    [HttpPatch("{id:int}/slots"), ValidateAntiForgeryToken, RequestSizeLimit(16384)]
    public Task<IActionResult> EditSlot(int id, EditScheduleSlotRequest input, [FromServices] LoadsheddingScheduleService service, CancellationToken ct) =>
        Run(async () => ScheduleResponse(await service.EditAsync(id, input, Actor(), ct), service));

    private int Actor() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0
        ? id : throw new ScheduleProblem(401, "Sign in again.");

    private async Task<IActionResult> Run(Func<Task<object>> action)
    {
        try { return Ok(await action()); }
        catch (ScheduleProblem ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Another official changed this schedule. Reload it." }); }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
        { return Conflict(new { message = "Another schedule operation is in progress. Reload before retrying." }); }
    }

    private static object ScheduleResponse(LoadsheddingSchedule row, LoadsheddingScheduleService service) => new
    {
        row.Id, row.StartDate, row.EndDate, row.AvailableCapacityMw, row.SlotMinutes,
        state = row.State.ToString(), row.Revision, row.CreatedAtUtc, row.PublishedAtUtc,
        timeZone = "Asia/Dhaka", review = service.Review(row),
        demands = row.Demands.OrderBy(x => x.ZoneId).Select(x => new { x.ZoneId, x.Zone.Code, x.Zone.Name, x.DemandMw }),
        slots = row.Slots.OrderBy(x => x.Date).ThenBy(x => x.StartMinute).ThenBy(x => x.ZoneId)
            .Select(x => new { x.Id, x.ZoneId, x.Date, x.StartMinute, x.EndMinute })
    };
}
