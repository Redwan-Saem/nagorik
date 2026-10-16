using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;
using Nagorik.Api.Services;

namespace Nagorik.Api.Controllers;

[ApiController]
[Authorize(Roles = OfficialAccess.Roles)]
[Route("api/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class OfficialReportsApiController(OfficialReportQueryService reports) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OfficialReportPage>> List([FromQuery] OfficialReportListQuery query, CancellationToken ct) =>
        Ok(await reports.ListAsync(query, ct));

    [HttpGet("/api/official/reports/{id:int}")]
    public async Task<ActionResult<OfficialReportDetailDto>> Detail([Range(1, int.MaxValue)] int id, CancellationToken ct)
    {
        var report = await reports.GetByIdAsync(id, ct);
        return report == null ? Problem(statusCode: 404, title: "Report not found.") : Ok(report);
    }
}
