using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
public sealed class BusOccupancyInput
{
    [Range(1,int.MaxValue)]public int LinkId{get;set;}
    [Required,RegularExpression("^(SeatsAvailable|Standing|Full)$")]public string Level{get;set;}="";
}
[ApiController,Route("api/transport/occupancy")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class BusOccupancyApiController(AppDbContext db,BusOccupancyService occupancy):ControllerBase
{
    [HttpGet("segments")]
    public async Task<IActionResult> Segments(CancellationToken ct)=>Ok(await (
        from link in db.TransportLinks.AsNoTracking() join a in db.TransportStops on link.FromStopId equals a.Id join b in db.TransportStops on link.ToStopId equals b.Id
        where link.Enabled&&link.Mode=="Bus"&&a.Enabled&&b.Enabled orderby link.Line,a.Name,b.Name
        select new{link.Id,link.Line,From=a.Name,To=b.Name,link.IsSample,link.Source}).Take(500).ToListAsync(ct));
    [HttpGet]
    public async Task<IActionResult> Summary([Required,Range(1,int.MaxValue)]int? linkId,CancellationToken ct)
    {try{return Ok(await occupancy.ReadAsync(linkId!.Value,ct));}catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}}
    [HttpGet("security-token"),Authorize(Roles="Resident")]
    public IActionResult Token([FromServices]Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)=>Ok(new{requestToken=antiforgery.GetAndStoreTokens(HttpContext).RequestToken});
    [HttpPost,Authorize(Roles="Resident"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromBody]BusOccupancyInput input,CancellationToken ct)
    {
        if(!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)||id<=0)return Unauthorized();
        try{await occupancy.SubmitAsync(id,input.LinkId,input.Level,ct);return Ok(await occupancy.ReadAsync(input.LinkId,ct));}
        catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}
    }
}
