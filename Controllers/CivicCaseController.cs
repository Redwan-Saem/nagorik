using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
namespace Nagorik.Api.Controllers;
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None),RequestSizeLimit(16*1024)]
public sealed class CivicCaseController(AppDbContext db,CivicCaseService service,TimeProvider clock):Controller
{
    private int Actor=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private async Task<CivicSubmitView> Model(string kind,CivicCaseInput? input,string? observed,CancellationToken ct)=>new(kind,input??new(){SubmissionToken=Guid.NewGuid().ToString()},await db.CivicCases.AsNoTracking().Where(x=>x.Kind==kind&&x.ResidentId==Actor).OrderByDescending(x=>x.Id).Take(50).ToListAsync(ct),observed??DhakaTime.ToLocal(clock.GetUtcNow().UtcDateTime).ToString("yyyy-MM-ddTHH:mm"));
    [HttpGet("/resident/concerns/noise"),Authorize(Roles="Resident")]
    public async Task<IActionResult> Noise(CancellationToken ct)=>View("Submit",await Model("Noise",null,null,ct));
    [HttpPost("/resident/concerns/noise"),Authorize(Roles="Resident"),ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitNoise([Bind(Prefix="Input")]CivicCaseInput input,string observedLocal,CancellationToken ct)=>Submit("Noise",input,observedLocal,ct);
    [HttpGet("/resident/concerns/construction"),Authorize(Roles="Resident")]
    public async Task<IActionResult> Construction(CancellationToken ct)=>View("Submit",await Model("Construction",null,null,ct));
    [HttpPost("/resident/concerns/construction"),Authorize(Roles="Resident"),ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitConstruction([Bind(Prefix="Input")]CivicCaseInput input,string observedLocal,CancellationToken ct)=>Submit("Construction",input,observedLocal,ct);
    private async Task<IActionResult> Submit(string kind,CivicCaseInput input,string observedLocal,CancellationToken ct)
    {
        ModelState.Remove("Input.ObservedAtUtc");
        if(DateTime.TryParseExact(observedLocal,"yyyy-MM-ddTHH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var local))input.ObservedAtUtc=DhakaTime.ToUtc(local);
        else ModelState.AddModelError("observedLocal","Choose the observation date and time in Asia/Dhaka.");
        try
        {
            if(ModelState.IsValid){var row=await service.SubmitAsync(kind,input,Actor,ct);return Redirect($"/resident/concerns/{kind.ToLowerInvariant()}/{row.Id}");}
            Response.StatusCode=400;
        }
        catch(TransportProblem e){if(e.Status==403)return Forbid();Response.StatusCode=e.Status;ModelState.AddModelError("",e.Message);}
        return View("Submit",await Model(kind,input,observedLocal,ct));
    }
    [HttpGet("/resident/concerns/noise/{id:int}"),Authorize(Roles="Resident")]
    public Task<IActionResult> NoiseDetail(int id,CancellationToken ct)=>Detail("Noise",id,false,ct);
    [HttpGet("/resident/concerns/construction/{id:int}"),Authorize(Roles="Resident")]
    public Task<IActionResult> ConstructionDetail(int id,CancellationToken ct)=>Detail("Construction",id,false,ct);
    private async Task<IActionResult> Detail(string kind,int id,bool official,CancellationToken ct)
    {try{return View("Detail",new CivicDetailView(await service.DetailAsync(kind,id,Actor,official,ct),official));}catch(TransportProblem e){return StatusCode(e.Status,e.Message);}}
    [HttpGet("/official/concerns/{kind}"),Authorize(Roles="Official,CityOfficial,Admin")]
    public async Task<IActionResult> Queue(string kind,CancellationToken ct)
    {
        try{var k=CivicCaseService.Kind(kind);await service.OfficialAsync(Actor,ct);return View(new CivicQueueView(k,await db.CivicCases.AsNoTracking().Where(x=>x.Kind==k).OrderByDescending(x=>x.Id).Take(100).ToListAsync(ct)));}
        catch(TransportProblem e){return StatusCode(e.Status,e.Message);}
    }
    [HttpGet("/official/concerns/{kind}/{id:int}"),Authorize(Roles="Official,CityOfficial,Admin")]
    public Task<IActionResult> OfficialDetail(string kind,int id,CancellationToken ct)=>Detail(kind,id,true,ct);
    [HttpPost("/official/concerns/{kind}/{id:int}/review"),Authorize(Roles="Official,CityOfficial,Admin"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string kind,int id,CivicReviewInput input,CancellationToken ct)
    {
        try{if(ModelState.IsValid){await service.ReviewAsync(kind,id,input,Actor,ct);return Redirect($"/official/concerns/{kind.ToLowerInvariant()}/{id}");}Response.StatusCode=400;}
        catch(TransportProblem e){if(e.Status is 403 or 404)return StatusCode(e.Status,e.Message);Response.StatusCode=e.Status;ModelState.AddModelError("",e.Message);}
        return View("Detail",new CivicDetailView(await service.DetailAsync(kind,id,Actor,true,ct),true));
    }
}
