using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Route("api/dispatch")]
[Authorize(Roles = "Dispatcher")]
public class DispatchApiController : ControllerBase
{
    private readonly CrewAssignmentService _service;

    public DispatchApiController(CrewAssignmentService service)
    {
        _service = service;
    }

    [HttpGet("crews")]
    public async Task<IActionResult> GetCrews()
    {
        var crews = await _service.GetCrewsAsync();
        return Ok(crews);
    }

    [HttpPost("reports/{id}/assign")]
    public async Task<IActionResult> Assign(
        int id,
        [FromBody] AssignCrewRequest request)
    {
        var dispatcherIdClaim =
            User.FindFirst("id")?.Value ??
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(dispatcherIdClaim, out var dispatcherId))
        {
            return Unauthorized(new { message = "Invalid dispatcher identity." });
        }

        var result = await _service.AssignAsync(
            id,
            request.CrewId,
            dispatcherId,
            request.ConfirmBusy);

        return result.Outcome switch
        {
            AssignOutcome.Assigned =>
                Ok(new { message = result.Message }),

            AssignOutcome.NeedsConfirmation =>
                Conflict(new
                {
                    needsConfirmation = true,
                    message = result.Message
                }),

            AssignOutcome.ReportNotFound =>
                NotFound(new { message = result.Message }),

            AssignOutcome.CrewNotFound =>
                NotFound(new { message = result.Message }),

            AssignOutcome.NotAssignable =>
                BadRequest(new { message = result.Message }),

            _ =>
                BadRequest(new { message = result.Message })
        };
    }
}

public record AssignCrewRequest(
    int CrewId,
    bool ConfirmBusy);