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
    [HttpGet("routes/{id}")]
    public async Task<IActionResult> Detail(string id,[FromQuery]RouteSearchInput input,CancellationToken ct)
    {
        if(id.Length!=20||!id.All(Uri.IsHexDigit))return BadRequest(new{message="Invalid route identifier."});
        try
        {
            var result=await search.SearchAsync(input,ct);
            var route=result.Options.SingleOrDefault(x=>x.Id==id);
            return route==null?NotFound(new{message="Route is no longer available. Search again."}):Ok(route);
        }
        catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}
    }
}
