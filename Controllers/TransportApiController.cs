using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;

[ApiController,Route("api/transport")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class TransportApiController(AppDbContext db,TransportSearchService search):ControllerBase
{
    [HttpGet("places")]
    public async Task<IActionResult> Places([FromQuery]string? q,CancellationToken ct)
    {
        if(q?.Length>100)return BadRequest(new{message="Use at most 100 characters."});
        var query=db.TransportStops.AsNoTracking().Where(x=>x.Enabled);
        if(!string.IsNullOrWhiteSpace(q))query=query.Where(x=>x.Name.Contains(q.Trim()));
        return Ok(await query.OrderBy(x=>x.Name).Take(100).Select(x=>new{x.Id,x.Name,x.Latitude,x.Longitude,x.Source,x.IsSample}).ToListAsync(ct));
    }
    [HttpGet("routes")]
    public async Task<IActionResult> Routes([FromQuery]RouteSearchInput input,CancellationToken ct)
    {
        try{return Ok(await search.SearchAsync(input,ct));}
        catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}
    }
}
