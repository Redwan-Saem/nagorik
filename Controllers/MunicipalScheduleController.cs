using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class MunicipalScheduleController(AppDbContext db,MunicipalScheduleService service,TimeProvider clock):Controller
{
    private int Actor=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private Task<List<ServiceZone>> Zones(CancellationToken ct)=>db.Zones.AsNoTracking().OrderBy(x=>x.Code).Select(x=>new ServiceZone(x.Id,x.Name)).ToListAsync(ct);
    [HttpGet("/services/water")]
    public Task<IActionResult> Water(int? zoneId,CancellationToken ct)=>Public("Water",zoneId,ct);
    private async Task<IActionResult> Public(string kind,int? zoneId,CancellationToken ct)
    {
        try{return View("Public",new MunicipalPublicView(kind,await Zones(ct),zoneId,zoneId.HasValue?await service.WeekAsync(kind,zoneId.Value,ct):null));}
        catch(TransportProblem e){return StatusCode(e.Status,e.Message);}
    }
    private async Task<MunicipalManageView> Model(string kind,int? id,MunicipalScheduleInput? input,CancellationToken ct)
    {
        await service.AuthorizeAsync(kind,Actor,ct);
        var items=await db.MunicipalSchedules.AsNoTracking().Where(x=>x.Kind==kind).OrderByDescending(x=>x.UpdatedAtUtc).ThenByDescending(x=>x.Id).Take(100).ToListAsync(ct);
        var row=id.HasValue?await db.MunicipalSchedules.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==kind,ct):null;
        if(id.HasValue&&row==null)throw new TransportProblem(404,"Schedule not found.");
        var today=DateOnly.FromDateTime(DhakaTime.ToLocal(clock.GetUtcNow().UtcDateTime));
        input??=row==null?new(){FromDate=today,ToDate=today.AddDays(27)}:new(){ZoneId=row.ZoneId,FromDate=row.FromDate,ToDate=row.ToDate,DaysMask=row.DaysMask,StartMinute=row.StartMinute,EndMinute=row.EndMinute,Source=row.Source,Notes=row.Notes,ExpectedRevision=row.Revision};
        return new(kind,await Zones(ct),items,id,input);
    }
    [HttpGet("/official/services/{kind}"),Authorize(Roles="UtilityOfficial,WASAOfficial,Official,CityOfficial,Admin")]
    public async Task<IActionResult> Manage(string kind,int? id,CancellationToken ct)
    {try{return View(await Model(MunicipalScheduleService.Kind(kind),id,null,ct));}catch(TransportProblem e){return StatusCode(e.Status,e.Message);}}
    [HttpPost("/official/services/{kind}/draft"),Authorize(Roles="UtilityOfficial,WASAOfficial,Official,CityOfficial,Admin"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Draft(string kind,int? id,[Bind(Prefix="Input")]MunicipalScheduleInput input,int[] days,string startTime,string endTime,CancellationToken ct)
    {
        try
        {
            var k=MunicipalScheduleService.Kind(kind);await service.AuthorizeAsync(k,Actor,ct);
            if(days.Length==0||days.Any(x=>x is not (1 or 2 or 4 or 8 or 16 or 32 or 64)))ModelState.AddModelError("days","Choose at least one valid weekday.");
            else input.DaysMask=days.Distinct().Aggregate(0,(mask,day)=>mask|day);
            if(!TimeOnly.TryParseExact(startTime,"HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var start))ModelState.AddModelError("startTime","Choose a valid start time.");else input.StartMinute=start.Hour*60+start.Minute;
            if(!TimeOnly.TryParseExact(endTime,"HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var end))ModelState.AddModelError("endTime","Choose a valid end time.");else input.EndMinute=end==TimeOnly.MinValue?1440:end.Hour*60+end.Minute;
            if(ModelState.IsValid){await service.SaveDraftAsync(k,id,input,Actor,ct);TempData["ServiceMessage"]="Draft saved. Review its source and window before publishing.";return RedirectToAction(nameof(Manage),new{kind=k.ToLowerInvariant()});}
            Response.StatusCode=400;return View("Manage",await Model(k,id,input,ct));
        }
        catch(TransportProblem e)
        {
            if(e.Status==403||e.Status==404)return StatusCode(e.Status,e.Message);
            Response.StatusCode=e.Status;ModelState.AddModelError("",e.Message);return View("Manage",await Model(MunicipalScheduleService.Kind(kind),id,input,ct));
        }
    }
    [HttpPost("/official/services/{kind}/{id:int}/state"),Authorize(Roles="UtilityOfficial,WASAOfficial,Official,CityOfficial,Admin"),ValidateAntiForgeryToken]
    public async Task<IActionResult> State(string kind,int id,int expectedRevision,bool publish,bool confirm,CancellationToken ct)
    {
        try{await service.ChangeStateAsync(kind,id,expectedRevision,publish,confirm,Actor,ct);TempData["ServiceMessage"]=publish?"Schedule published.":"Schedule retired.";return RedirectToAction(nameof(Manage),new{kind});}
        catch(TransportProblem e){return StatusCode(e.Status,e.Message+" Return to schedules and reload before retrying.");}
        catch(Microsoft.Data.Sqlite.SqliteException e)when(e.SqliteErrorCode is 5 or 6){return Conflict("Schedule database busy. Return to schedules and reload before retrying.");}
    }
}
