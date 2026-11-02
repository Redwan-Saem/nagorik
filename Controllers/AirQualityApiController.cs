using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ApiController,Route("api/air-quality"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AirQualityApiController(AirQualityService service):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([Required,Range(1,int.MaxValue)]int? zoneId,CancellationToken ct)
    {try{return Ok(await service.GetAsync(zoneId!.Value,ct));}catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}}
}
