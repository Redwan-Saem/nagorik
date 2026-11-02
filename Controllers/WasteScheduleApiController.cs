using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ApiController,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class WasteScheduleApiController(MunicipalScheduleService service):ControllerBase
{
    [HttpGet("/api/services/waste")]
    public async Task<IActionResult> Waste([Required,Range(1,int.MaxValue)]int? zoneId,CancellationToken ct)
    {try{return Ok(await service.WeekAsync("Waste",zoneId!.Value,ct));}catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}}
}
