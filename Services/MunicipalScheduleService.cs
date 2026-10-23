using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Services;
public sealed class MunicipalScheduleService(AppDbContext db,TimeProvider clock)
{
    public static string Kind(string value)=>value.ToLowerInvariant() switch{"water"=>"Water","waste"=>"Waste",_=>throw new TransportProblem(404,"Service not found.")};
    public static bool CanManage(string kind,string role)=>role=="Admin"||(kind=="Water"?role is "UtilityOfficial" or "WASAOfficial":role is "Official" or "CityOfficial");
    public async Task AuthorizeAsync(string kind,int actor,CancellationToken ct=default)
    {
        var role=await db.Users.Where(x=>x.Id==actor).Select(x=>x.Role).SingleOrDefaultAsync(ct);
        if(role==null||!CanManage(kind,role))throw new TransportProblem(403,"This official cannot manage this service.");
    }
    public async Task<MunicipalSchedule> SaveDraftAsync(string rawKind,int? id,MunicipalScheduleInput input,int actor,CancellationToken ct=default)
    {
        var kind=Kind(rawKind);await AuthorizeAsync(kind,actor,ct);
        var errors=new List<ValidationResult>();
        if(!Validator.TryValidateObject(input,new(input),errors,true))throw new TransportProblem(400,string.Join(" ",errors.Select(x=>x.ErrorMessage)));
        if(input.ToDate<input.FromDate||input.ToDate!.Value.DayNumber-input.FromDate!.Value.DayNumber>366||input.EndMinute<=input.StartMinute||string.IsNullOrWhiteSpace(input.Source))throw new TransportProblem(400,"Use a valid period of at most 367 days, a nonempty source and a same-day time window. Split overnight service into two windows.");
        if(!await db.Zones.AnyAsync(x=>x.Id==input.ZoneId,ct))throw new TransportProblem(400,"Choose an existing zone.");
        var row=id.HasValue?await db.MunicipalSchedules.SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==kind,ct):new MunicipalSchedule{Kind=kind};
        if(row==null)throw new TransportProblem(404,"Schedule not found.");
        if(id.HasValue&&(row.State!="Draft"||row.Revision!=input.ExpectedRevision))throw new TransportProblem(409,"Draft changed or was published. Reload before editing.");
        row.ZoneId=input.ZoneId;row.FromDate=input.FromDate.Value;row.ToDate=input.ToDate.Value;row.DaysMask=input.DaysMask;row.StartMinute=input.StartMinute;row.EndMinute=input.EndMinute;row.Source=input.Source.Trim();row.Notes=input.Notes?.Trim()??"";row.UpdatedByUserId=actor;row.UpdatedAtUtc=clock.GetUtcNow().UtcDateTime;
        if(id.HasValue)row.Revision++;else db.MunicipalSchedules.Add(row);
        await Save(ct);return row;
    }
    public async Task ChangeStateAsync(string rawKind,int id,int expected,bool publish,bool confirm,int actor,CancellationToken ct=default)
    {
        var kind=Kind(rawKind);await AuthorizeAsync(kind,actor,ct);if(!confirm)throw new TransportProblem(400,"Explicit confirmation is required.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var row=await db.MunicipalSchedules.SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==kind,ct)??throw new TransportProblem(404,"Schedule not found.");
        if(row.Revision!=expected||(publish?row.State!="Draft":row.State!="Published"))throw new TransportProblem(409,"Schedule changed or has a different state. Reload.");
        if(publish&&await db.MunicipalSchedules.AnyAsync(x=>x.Id!=id&&x.Kind==kind&&x.ZoneId==row.ZoneId&&x.State=="Published"&&x.FromDate<=row.ToDate&&x.ToDate>=row.FromDate&&(x.DaysMask&row.DaysMask)!=0&&x.StartMinute<row.EndMinute&&x.EndMinute>row.StartMinute,ct))throw new TransportProblem(409,"This window overlaps a published schedule. Retire the old window or adjust this draft.");
        row.State=publish?"Published":"Retired";row.Revision++;row.UpdatedByUserId=actor;row.UpdatedAtUtc=clock.GetUtcNow().UtcDateTime;if(publish)row.PublishedAtUtc=row.UpdatedAtUtc;
        await Save(ct);await tx.CommitAsync(ct);
    }
    private async Task Save(CancellationToken ct){try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){throw new TransportProblem(409,"Schedule changed. Reload before retrying.");}}
    public async Task<MunicipalWeek> WeekAsync(string rawKind,int zoneId,CancellationToken ct=default)
    {
        var kind=Kind(rawKind);var zone=await db.Zones.AsNoTracking().Where(x=>x.Id==zoneId).Select(x=>x.Name).SingleOrDefaultAsync(ct)??throw new TransportProblem(404,"Zone not found.");
        var today=DateOnly.FromDateTime(DhakaTime.ToLocal(clock.GetUtcNow().UtcDateTime));var end=today.AddDays(6);
        var rows=await db.MunicipalSchedules.AsNoTracking().Where(x=>x.Kind==kind&&x.ZoneId==zoneId&&x.State=="Published"&&x.PublishedAtUtc!=null&&x.FromDate<=end&&x.ToDate>=today).OrderBy(x=>x.StartMinute).ToListAsync(ct);
        var days=Enumerable.Range(0,7).Select(i=>
        {
            var date=today.AddDays(i);var active=rows.Where(x=>x.FromDate<=date&&x.ToDate>=date).ToArray();
            return new MunicipalDay(date,active.Length>0,active.Where(x=>(x.DaysMask&(1<<(int)date.DayOfWeek))!=0).Select(x=>new MunicipalWindow(x.StartMinute,x.EndMinute,x.Source,x.Notes,DateTime.SpecifyKind(x.PublishedAtUtc!.Value,DateTimeKind.Utc))).ToArray());
        }).ToArray();return new(kind,zoneId,zone,"Asia/Dhaka",days);
    }
}
