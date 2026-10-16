using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Route("api/crew/tasks")]
[Authorize(Roles = "Crew")]
public class CrewTasksApiController(ReportStatusWorkflowService workflow) : ControllerBase
{
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id, [FromBody] UpdateCrewTaskStatusRequest request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return Problem(statusCode: 401, title: "Invalid crew identity.");

        // Accept names only; numeric enum values and other workflow states aren't crew commands.
        var next = request.Status switch
        {
            "InProgress" => ReportStatus.InProgress,
            "Completed" => ReportStatus.Completed,
            _ => (ReportStatus?)null
        };
        if (next == null)
            return Problem(statusCode: 400, title: "Invalid task status.", detail: "Status must be InProgress or Completed.");

        var result = await workflow.ChangeCrewTaskStatusAsync(id, next.Value, userId, ct);
        return result.Outcome switch
        {
            StatusChangeOutcome.Changed => Ok(new { id, status = next.Value.ToString(), message = result.Message }),
            StatusChangeOutcome.ReportNotFound => Problem(statusCode: 404, title: "Task not found.", detail: result.Message),
            StatusChangeOutcome.InvalidTransition =>
                Problem(statusCode: 409, title: "Task update conflict.", detail: result.Message),
            _ => StatusCode(500)
        };
    }
}

public sealed class UpdateCrewTaskStatusRequest
{
    public string Status { get; set; } = "";
}
