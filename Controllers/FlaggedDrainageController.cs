using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;
public sealed record FlaggedDrainageViewModel(IReadOnlyList<FlaggedDrainagePoint> Points, IReadOnlyList<CrewDto> Crews);
public sealed class QuickDrainageAssignment
{
    [Range(1, int.MaxValue)] public int CrewId { get; set; }
    public bool ConfirmBusy { get; set; }
}
[Authorize(Policy = DispatcherMapAccess.Policy), Route("dispatcher/flagged-drainage")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FlaggedDrainageController(FlaggedDrainageQueryService points, CrewAssignmentService crews, DrainageDispatchService dispatch) : Controller
{
    [HttpGet("")] public async Task<IActionResult> Index(CancellationToken ct) => View(new FlaggedDrainageViewModel(
        (await points.ListAsync(ct)).Take(100).ToList(), await crews.GetCrewsAsync()));
    [HttpPost("{id:int}/assign"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, QuickDrainageAssignment input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0) return Unauthorized();
        var result = await dispatch.AssignAsync(id, input.CrewId, userId, input.ConfirmBusy, ct);
        if (result.Outcome == AssignOutcome.Assigned) return Redirect($"/Dispatch/Report/{result.ReportId}");
        TempData["DrainageMessage"] = result.Message + (result.ReportId.HasValue ? $" Open report R-{result.ReportId:D6} to inspect existing work." : "");
        return RedirectToAction(nameof(Index));
    }
}
