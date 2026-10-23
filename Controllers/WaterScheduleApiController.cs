using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ApiController,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class WaterScheduleApiController(AppDbContext db,MunicipalScheduleService service):ControllerBase
{
    [HttpGet("/api/services/zones")]
    public async Task<IActionResult> Zones(CancellationToken ct)=>Ok(await db.Zones.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new{x.Id,x.Code,x.Name}).ToListAsync(ct));
    [HttpGet("/api/services/water")]
    public async Task<IActionResult> Water([Required,Range(1,int.MaxValue)]int? zoneId,CancellationToken ct)
    {try{return Ok(await service.WeekAsync("Water",zoneId!.Value,ct));}catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}}
}
