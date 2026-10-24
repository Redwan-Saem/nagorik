using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ApiController,Authorize(Roles="Admin"),Route("api/admin/data"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None),RequestSizeLimit(2*1024*1024)]
public sealed class ManagedDataApiController(AppDbContext db,GeographicDataService geography,TransportCatalogService catalog):ControllerBase
{
    private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"unknown";
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {try{return await action();}catch(TransportProblem e){return StatusCode(e.Status,new{message=e.Message});}catch(Microsoft.Data.Sqlite.SqliteException e)when(e.SqliteErrorCode is 5 or 6){return Conflict(new{message="Data is being updated. Reload before retrying."});}}
    [HttpGet("security-token")]
    public IActionResult Token([FromServices]Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)=>Ok(new{requestToken=antiforgery.GetAndStoreTokens(HttpContext).RequestToken});
    [HttpGet("zones")]
    public async Task<IActionResult> Zones(CancellationToken ct)=>Ok(await db.Zones.AsNoTracking().OrderBy(x=>x.Code).ToListAsync(ct));
    [HttpPost("zones"),ValidateAntiForgeryToken]
    public Task<IActionResult> Create(ZoneDataInput input,CancellationToken ct)=>Run(async()=>Ok(await geography.SaveZoneAsync(null,input,Actor,ct)));
    [HttpPut("zones/{id:int}"),ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id,ZoneDataInput input,CancellationToken ct)=>Run(async()=>Ok(await geography.SaveZoneAsync(id,input,Actor,ct)));
    [HttpGet("transport")]
    public async Task<IActionResult> Export(CancellationToken ct)=>Ok(await catalog.ExportAsync(ct));
    [HttpPost("transport"),ValidateAntiForgeryToken]
    public Task<IActionResult> Import(TransportCatalogDocument input,CancellationToken ct)=>Run(async()=>Ok(new{revision=await catalog.ImportAsync(input,Actor,ct)}));
    [HttpGet("changes")]
    public async Task<IActionResult> Changes(CancellationToken ct)=>Ok(await db.ManagedDataChanges.AsNoTracking().OrderByDescending(x=>x.Id).Take(100).Select(x=>new{x.Id,x.Kind,x.ZoneId,x.Source,x.IsSample,x.ActorUserId,x.AtUtc}).ToListAsync(ct));
    [HttpGet("changes/{id:long}")]
    public async Task<IActionResult> Change(long id,CancellationToken ct)
    {var row=await db.ManagedDataChanges.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);return row==null?NotFound():Ok(row);}
}
