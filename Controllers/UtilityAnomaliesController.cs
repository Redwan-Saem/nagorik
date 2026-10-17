using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Authorize(Roles = "Resident")]
[Route("api/reports/utility-anomalies")]
public sealed class UtilityAnomaliesController(UtilityAnomalySubmissionService submissions) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(17_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 17_000_000)]
    public async Task<IActionResult> Create([FromForm] CreateUtilityAnomalyRequest request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Unauthorized();
        var result = await submissions.CreateAsync(userId, request, ct);
        if (result.InvalidIdentity) return Unauthorized();
        if (result.Report == null)
        {
            foreach (var error in result.Errors)
                foreach (var message in error.Value) ModelState.AddModelError(error.Key, message);
            return ValidationProblem(ModelState);
        }
        return Created($"/api/reports/{result.Report.Id}", result.Report);
    }
}
