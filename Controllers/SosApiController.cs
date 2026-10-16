using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sos")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SosApiController(SosAlertService service) : ControllerBase
{
    private bool TryUserId(out int id) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id) && id > 0;

    [HttpPost]
    public async Task<IActionResult> Trigger([FromBody] SosTriggerRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        return Respond(await service.TriggerAsync(userId, request, ct));
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var alert = await service.GetActiveAsync(userId, ct);
        return alert == null ? NoContent() : Ok(alert);
    }

    [HttpGet("requests/{requestId:guid}")]
    public async Task<IActionResult> GetByRequestId(Guid requestId, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        if (requestId == Guid.Empty) return Problem(statusCode: 400, title: "Invalid submission reference.");
        var alert = await service.GetByRequestIdAsync(requestId, userId, ct);
        return alert == null ? Problem(statusCode: 404, title: "SOS submission not found.") : Ok(alert);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([Range(1, int.MaxValue)] int id, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        var alert = await service.GetByIdAsync(id, userId, ct);
        return alert == null ? Problem(statusCode: 404, title: "SOS alert not found.") : Ok(alert);
    }

    [HttpPatch("{id:int}/location")]
    public async Task<IActionResult> UpdateLocation([Range(1, int.MaxValue)] int id,
        [FromBody] SosLocationRequest request, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        return Respond(await service.UpdateLocationAsync(id, userId, request, ct));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel([Range(1, int.MaxValue)] int id, CancellationToken ct)
    {
        if (!TryUserId(out var userId)) return Unauthorized();
        return Respond(await service.CancelAsync(id, userId, ct));
    }

    private IActionResult Respond(SosResult result) => result.Outcome switch
    {
        SosOutcome.Created => CreatedAtAction(nameof(GetById), new { id = result.Alert!.Id }, result.Alert),
        SosOutcome.Success => Ok(result.Alert),
        SosOutcome.Invalid => ValidationProblem(new ValidationProblemDetails(result.Errors!)),
        SosOutcome.NotFound => Problem(statusCode: 404, title: "SOS alert not found."),
        SosOutcome.UserNotFound => Problem(statusCode: 401, title: "Your account is no longer available."),
        SosOutcome.Conflict => ConflictProblem(result.Alert),
        SosOutcome.RequestConflict => Problem(statusCode: 409, title: "Submission reference already used.",
            detail: "This reference belongs to an earlier request with a different location. Check that alert before submitting again."),
        _ => StatusCode(500)
    };

    private IActionResult ConflictProblem(SosAlertDto? active)
    {
        var problem = new ProblemDetails
        {
            Status = 409, Title = "SOS alert conflict.",
            Detail = active != null ? "You already have an active SOS alert." :
                "The alert is no longer active or changed during this request. Refresh it before trying again."
        };
        if (active != null) problem.Extensions["activeAlertId"] = active.Id;
        return new ObjectResult(problem) { StatusCode = 409 };
    }
}
