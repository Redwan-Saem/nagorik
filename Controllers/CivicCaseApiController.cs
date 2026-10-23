using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ApiController,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None),RequestSizeLimit(16*1024)]
public sealed class CivicCaseApiController(AppDbContext db,CivicCaseService service):ControllerBase
{
    private int Actor=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private static DateTime Utc(DateTime value)=>DateTime.SpecifyKind(value,DateTimeKind.Utc);
    private static object Safe(CivicCase x)=>new{x.Id,ReferenceCode=$"{(x.Kind=="Noise"?"NC":"CC")}-{x.Id:000000}",x.Kind,x.Subtype,x.Description,x.Address,x.Latitude,x.Longitude,ObservedAtUtc=Utc(x.ObservedAtUtc),x.ReportedDecibels,x.Reference,x.State,x.Revision,CreatedAtUtc=Utc(x.CreatedAtUtc)};
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {try{return await action();}catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}catch(Microsoft.Data.Sqlite.SqliteException e)when(e.SqliteErrorCode is 5 or 6){return Conflict(new{message="Database busy. Reload before retrying."});}}
    [HttpGet("/api/resident/concerns/security-token"),Authorize(Roles="Resident")]
    public IActionResult Token([FromServices]Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)=>Ok(new{requestToken=antiforgery.GetAndStoreTokens(HttpContext).RequestToken});
    [HttpGet("/api/official/concerns/security-token"),Authorize(Roles="Official,CityOfficial,Admin")]
    public IActionResult OfficialToken([FromServices]Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)=>Token(antiforgery);
    [HttpGet("/api/resident/concerns/noise"),Authorize(Roles="Resident")]
    public Task<IActionResult> NoiseList(CancellationToken ct)=>ResidentList("Noise",ct);
    [HttpGet("/api/resident/concerns/noise/{id:int}"),Authorize(Roles="Resident")]
    public Task<IActionResult> NoiseDetail(int id,CancellationToken ct)=>Detail("Noise",id,false,ct);
    [HttpPost("/api/resident/concerns/noise"),Authorize(Roles="Resident"),ValidateAntiForgeryToken]
    public Task<IActionResult> NoiseSubmit(CivicCaseInput input,CancellationToken ct)=>Submit("Noise",input,ct);
    [HttpGet("/api/resident/concerns/construction"),Authorize(Roles="Resident")]
    public Task<IActionResult> ConstructionList(CancellationToken ct)=>ResidentList("Construction",ct);
    [HttpGet("/api/resident/concerns/construction/{id:int}"),Authorize(Roles="Resident")]
    public Task<IActionResult> ConstructionDetail(int id,CancellationToken ct)=>Detail("Construction",id,false,ct);
    [HttpPost("/api/resident/concerns/construction"),Authorize(Roles="Resident"),ValidateAntiForgeryToken]
    public Task<IActionResult> ConstructionSubmit(CivicCaseInput input,CancellationToken ct)=>Submit("Construction",input,ct);
    private Task<IActionResult> ResidentList(string kind,CancellationToken ct)=>Run(async()=>
    {var rows=await db.CivicCases.AsNoTracking().Where(x=>x.ResidentId==Actor&&x.Kind==kind).OrderByDescending(x=>x.Id).Take(50).ToListAsync(ct);return Ok(rows.Select(Safe));});
    private Task<IActionResult> Submit(string kind,CivicCaseInput input,CancellationToken ct)=>Run(async()=>Ok(Safe(await service.SubmitAsync(kind,input,Actor,ct))));
    private Task<IActionResult> Detail(string kind,int id,bool official,CancellationToken ct)=>Run(async()=>{var data=await service.DetailAsync(kind,id,Actor,official,ct);return Ok(new{concern=Safe(data.Case),history=data.Reviews.Select(x=>new{x.State,x.Note,AtUtc=Utc(x.AtUtc)})});});
    [HttpGet("/api/official/concerns/{kind}"),Authorize(Roles="Official,CityOfficial,Admin")]
    public Task<IActionResult> Queue(string kind,string? state,CancellationToken ct)=>Run(async()=>
    {
        var k=CivicCaseService.Kind(kind);await service.OfficialAsync(Actor,ct);
        if(state!=null&&state is not ("Pending" or "UnderReview" or "Resolved" or "Rejected"))return BadRequest(new{message="Invalid review state."});
        var query=db.CivicCases.AsNoTracking().Where(x=>x.Kind==k);if(state!=null)query=query.Where(x=>x.State==state);
        return Ok((await query.OrderByDescending(x=>x.Id).Take(100).ToListAsync(ct)).Select(Safe));
    });
    [HttpGet("/api/official/concerns/{kind}/{id:int}"),Authorize(Roles="Official,CityOfficial,Admin")]
    public Task<IActionResult> OfficialDetail(string kind,int id,CancellationToken ct)=>Detail(kind,id,true,ct);
    [HttpPost("/api/official/concerns/{kind}/{id:int}/review"),Authorize(Roles="Official,CityOfficial,Admin"),ValidateAntiForgeryToken]
    public Task<IActionResult> Review(string kind,int id,CivicReviewInput input,CancellationToken ct)=>Run(async()=>{await service.ReviewAsync(kind,id,input,Actor,ct);return Ok(new{message="Review saved."});});
}
