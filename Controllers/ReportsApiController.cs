using Microsoft.EntityFrameworkCore;
using Nagorik.Api;
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
    private readonly AppDbContext _db;

    public ReportsApiController(
        IReportService service,
        AppDbContext db)
    {
        _service = service;
        _db = db;
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
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userIdClaim =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var report = await _db.Reports
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.Id == id && r.UserId == userId);

            if (report == null)
                return NotFound(new
                {
                    message = "Report not found."
                });

            var history = await _db.ReportStatusHistories
                .AsNoTracking()
                .Where(h => h.ReportId == report.Id)
                .OrderBy(h => h.ChangedAtUtc)
                .ToListAsync();

            return Ok(new
            {
                id = report.Id,
                code = $"R-{report.Id:D6}",
                title = report.Title,
                description = report.Description,
                category = report.Category.ToString(),
                status = report.Status.ToString(),
                location = report.Location,
                createdAt = report.CreatedAt,

                statusHistory = history.Select(h => new
                {
                    status = h.Status.ToString(),
                    changedAtUtc = h.ChangedAtUtc
                }).ToList()
            });
        }
    }

