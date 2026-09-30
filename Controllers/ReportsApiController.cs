using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Resident")]
public class ReportsApiController : ControllerBase
{
    private readonly IReportService _service;

    public ReportsApiController(IReportService service)
    {
        _service = service;
    }

    [HttpPost]
    [RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Create(
        [FromForm] CreateReportRequest request)
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var result = await _service.CreateAsync(
            userId,
            request);

        if (!result.Success)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(e.Key, e.Value);

            return ValidationProblem(ModelState);
        }

        return Ok(new
        {
            id = result.ReportId,
            code = $"R-{result.ReportId:D6}"
        });
    }
}