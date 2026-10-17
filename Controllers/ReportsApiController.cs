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
    [HttpGet("mine")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Mine([FromQuery] ResidentReportListQuery request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0 ||
            !await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == "Resident", ct))
            return Unauthorized();
        var query = _db.Reports.AsNoTracking().Where(r => r.UserId == userId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((request.Page - 1) * ResidentReportListQuery.PageSize).Take(ResidentReportListQuery.PageSize)
            .Select(r => new ResidentReportListItem(r.Id, r.ReferenceNo ?? $"R-{r.Id:D6}", r.ReferenceNo,
                r.Title, r.Category.ToString(), r.AnomalyType.HasValue ? r.AnomalyType.Value.ToString() : null,
                r.Utility.HasValue ? r.Utility.Value.ToString() : null, ResidentReportStatus.GetLabel(r.Status),
                r.Urgency.ToString(), r.AreaId, r.Area == null ? null : r.Area.Name, r.Location, r.CreatedAt))
            .ToListAsync(ct);
        return Ok(new ResidentReportPage(items, request.Page, ResidentReportListQuery.PageSize, count,
            (int)Math.Ceiling(count / (double)ResidentReportListQuery.PageSize)));
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
                displayStatus = ResidentReportStatus.GetLabel(report.Status),
                location = report.Location,
                createdAt = report.CreatedAt,

                statusHistory = history.Select(h => new
                {
                    status = h.Status.ToString(),
                    displayStatus = ResidentReportStatus.GetLabel(h.Status),
                    changedAtUtc = h.ChangedAtUtc
                }).ToList()
            });
        }
    }

