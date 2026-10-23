using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[Authorize(Roles="Admin"),Route("admin/data"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None),RequestSizeLimit(2*1024*1024)]
public sealed class ManagedDataController(AppDbContext db,GeographicDataService geography,TransportCatalogService catalog):Controller
{
    private string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)??"unknown";
    private async Task<ManagedDataView> Model(int? id,ZoneDataInput? input,CancellationToken ct)
    {
        if(id.HasValue&&input==null)
        {var row=await db.Zones.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new TransportProblem(404,"Zone not found.");input=new(){Code=row.Code,Name=row.Name,BoundaryGeoJson=row.BoundaryGeoJson,ElevationMeters=row.ElevationMeters,DrainageDensityKmPerSqKm=row.DrainageDensityKmPerSqKm,HistoryScore=row.HistoryScore,Source=row.BoundarySource??"",IsSample=row.BoundaryIsSample??false,ExpectedRevision=row.GeoRevision};}
        return new(id,input??new(),await db.Zones.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new ManagedZoneSummary(x.Id,x.Code,x.Name,x.GeoRevision,x.BoundarySource,x.BoundaryIsSample,x.InputSource,x.InputIsSample)).ToListAsync(ct),await db.ManagedDataChanges.AsNoTracking().OrderByDescending(x=>x.Id).Take(100).Select(x=>new ManagedChangeSummary(x.Id,x.Kind,x.ZoneId,x.Source,x.IsSample,x.ActorUserId,x.AtUtc)).ToListAsync(ct));
    }
    [HttpGet("")]
    public async Task<IActionResult> Index(int? id,CancellationToken ct)
    {try{return View(await Model(id,null,ct));}catch(TransportProblem e){return StatusCode(e.Status,e.Message);}}
    [HttpPost("zone"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id,[Bind(Prefix="Input")]ZoneDataInput input,CancellationToken ct)
    {
        try
        {if(ModelState.IsValid){var row=await geography.SaveZoneAsync(id,input,Actor,ct);TempData["DataMessage"]="Geographic data saved with source/history. Old risk was cleared; collect current weather and evaluate again.";return RedirectToAction(nameof(Index),new{id=row.Id});}Response.StatusCode=400;}
        catch(TransportProblem e){Response.StatusCode=e.Status;ModelState.AddModelError("",e.Message);}
        catch(Microsoft.Data.Sqlite.SqliteException e)when(e.SqliteErrorCode is 5 or 6){Response.StatusCode=409;ModelState.AddModelError("","Data is being updated. Reload before retrying.");}
        return View("Index",await Model(id,input,ct));
    }
    [HttpGet("transport/export")]
    public async Task<IActionResult> Export(CancellationToken ct)=>File(JsonSerializer.SerializeToUtf8Bytes(await catalog.ExportAsync(ct),new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}),"application/json","transport-catalog.json");
    [HttpPost("transport/import"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string catalogJson,CancellationToken ct)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(catalogJson)||catalogJson.Length>1800000)throw new TransportProblem(400,"Paste a catalog JSON document under 1.8 MB.");
            var input=JsonSerializer.Deserialize<TransportCatalogDocument>(catalogJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))??throw new TransportProblem(400,"Catalog document is required.");
            var revision=await catalog.ImportAsync(input,Actor,ct);TempData["DataMessage"]=$"Transport catalog imported at revision {revision}. Omitted rows were disabled, not deleted.";return RedirectToAction(nameof(Index));
        }
        catch(JsonException){Response.StatusCode=400;ModelState.AddModelError("","Catalog JSON is invalid.");}
        catch(TransportProblem e){Response.StatusCode=e.Status;ModelState.AddModelError("",e.Message);}
        catch(Microsoft.Data.Sqlite.SqliteException e)when(e.SqliteErrorCode is 5 or 6){Response.StatusCode=409;ModelState.AddModelError("","Data is being updated. Export again before retrying.");}
        ViewData["CatalogJson"]=catalogJson;return View("Index",await Model(null,null,ct));
    }
}
