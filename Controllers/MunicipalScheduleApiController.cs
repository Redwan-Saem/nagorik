using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
public sealed class MunicipalStateInput
{
    [Range(1,int.MaxValue)]public int ExpectedRevision{get;set;}
    [Required]public bool? Confirm{get;set;}
}
[ApiController,Authorize(Roles="UtilityOfficial,WASAOfficial,Official,CityOfficial,Admin"),Route("api/official/services")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class MunicipalScheduleApiController(AppDbContext db,MunicipalScheduleService service):ControllerBase
{
    private int Actor=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try{return await action();}
        catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}
        catch(Microsoft.Data.Sqlite.SqliteException e) when(e.SqliteErrorCode is 5 or 6){return Conflict(new{message="The schedule database is busy. Reload and retry."});}
    }
    [HttpGet("security-token")]
    public IActionResult Token([FromServices]Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)=>Ok(new{requestToken=antiforgery.GetAndStoreTokens(HttpContext).RequestToken});
    [HttpGet("{kind}")]
    public Task<IActionResult> List(string kind,CancellationToken ct)=>Run(async()=>
    {var k=MunicipalScheduleService.Kind(kind);await service.AuthorizeAsync(k,Actor,ct);return Ok(await db.MunicipalSchedules.AsNoTracking().Where(x=>x.Kind==k).OrderByDescending(x=>x.UpdatedAtUtc).ThenByDescending(x=>x.Id).Take(100).ToListAsync(ct));});
    [HttpPost("{kind}/drafts"),ValidateAntiForgeryToken]
    public Task<IActionResult> Create(string kind,MunicipalScheduleInput input,CancellationToken ct)=>Run(async()=>Ok(await service.SaveDraftAsync(kind,null,input,Actor,ct)));
    [HttpPut("{kind}/drafts/{id:int}"),ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(string kind,int id,MunicipalScheduleInput input,CancellationToken ct)=>Run(async()=>Ok(await service.SaveDraftAsync(kind,id,input,Actor,ct)));
    [HttpPost("{kind}/{id:int}/publish"),ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(string kind,int id,MunicipalStateInput input,CancellationToken ct)=>Run(async()=>{await service.ChangeStateAsync(kind,id,input.ExpectedRevision,true,input.Confirm!.Value,Actor,ct);return Ok(new{message="Schedule published."});});
    [HttpPost("{kind}/{id:int}/retire"),ValidateAntiForgeryToken]
    public Task<IActionResult> Retire(string kind,int id,MunicipalStateInput input,CancellationToken ct)=>Run(async()=>{await service.ChangeStateAsync(kind,id,input.ExpectedRevision,false,input.Confirm!.Value,Actor,ct);return Ok(new{message="Schedule retired."});});
}
