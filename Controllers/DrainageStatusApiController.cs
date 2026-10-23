using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers;

public sealed class DrainageStatusRequest
{
    [Required, RegularExpression("^(Clear|Blocked)$")]
    public string Status { get; set; } = "";
    [Required, RegularExpression("^(Unknown|Clear|Blocked)$")]
    public string ExpectedStatus { get; set; } = "";
}

[ApiController]
[Authorize(Policy = DispatcherMapAccess.Policy)]
[Route("api/map/drainage-points")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DrainageStatusApiController(AppDbContext db) : ControllerBase
{
    [HttpPatch("{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update([Range(1, int.MaxValue)] int id,
        [FromBody] DrainageStatusRequest request, CancellationToken ct)
    {
        var next = Enum.Parse<DrainageStatus>(request.Status);
        var expected = Enum.Parse<DrainageStatus>(request.ExpectedStatus);
        // Atomic compare-and-set: reject an editor whose displayed status is stale.
        var changed = await db.DrainagePoints.Where(p => p.Id == id && p.Status == expected)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.Status, next), ct);
        if (changed == 1) return Ok(new { id, status = next.ToString() });
        var current = await db.DrainagePoints.AsNoTracking().Where(p => p.Id == id).Select(p => (DrainageStatus?)p.Status).SingleOrDefaultAsync(ct);
        return current == null ? NotFound(new { message = "Drainage point not found." }) :
            Conflict(new { message = "This point changed since it was loaded. Refresh the map before updating it.", currentStatus = current.Value.ToString() });
    }
}
