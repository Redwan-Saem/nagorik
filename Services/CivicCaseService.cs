using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Services;
public sealed class CivicCaseService(AppDbContext db,TimeProvider clock)
{
    public static string Kind(string value)=>value.ToLowerInvariant() switch{"noise"=>"Noise","construction"=>"Construction",_=>throw new TransportProblem(404,"Complaint type not found.")};
    public static string[] Subtypes(string kind)=>kind=="Noise"?["Traffic","Construction","Commercial","Other"]:["SafetyHazard","Dust","Noise","ObstructedFootpath","Other"];
    private static void Validate(object input){var errors=new List<ValidationResult>();if(!Validator.TryValidateObject(input,new(input),errors,true))throw new TransportProblem(400,string.Join(" ",errors.Select(e=>e.ErrorMessage)));}
    public async Task<CivicCase> SubmitAsync(string rawKind,CivicCaseInput input,int resident,CancellationToken ct=default)
    {
        var kind=Kind(rawKind);Validate(input);
        if(!await db.Users.AnyAsync(x=>x.Id==resident&&x.Role=="Resident",ct))throw new TransportProblem(403,"A provisioned resident account is required.");
        var now=clock.GetUtcNow().UtcDateTime;
        if(!Subtypes(kind).Contains(input.Subtype)||!Guid.TryParseExact(input.SubmissionToken,"D",out _)||string.IsNullOrWhiteSpace(input.Description)||input.Description.Trim().Length<10||string.IsNullOrWhiteSpace(input.Address))throw new TransportProblem(400,"Choose a valid concern type, description, address and submission token.");
        if(!double.IsFinite(input.Latitude!.Value)||!double.IsFinite(input.Longitude!.Value)||!ReportValidator.IsInDhaka(input.Latitude.Value,input.Longitude.Value))throw new TransportProblem(400,"Choose a location inside the Dhaka service area.");
        if(input.ObservedAtUtc!.Value.Kind!=DateTimeKind.Utc||input.ObservedAtUtc>now.AddMinutes(2)||input.ObservedAtUtc<now.AddDays(-30))throw new TransportProblem(400,"Observation must use UTC and be within the last 30 days, not in the future.");
        if(input.ReportedDecibels is {} decibels&&(!double.IsFinite(decibels)||kind!="Noise"))throw new TransportProblem(400,"Decibels are optional resident-reported values for noise concerns only.");
        input.SubmissionToken=Guid.Parse(input.SubmissionToken).ToString("D");
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{kind,input.Subtype,Description=input.Description.Trim(),Address=input.Address.Trim(),input.Latitude,input.Longitude,input.ObservedAtUtc,input.ReportedDecibels,Reference=input.Reference?.Trim()}))));
        await using var transaction=await Begin(ct);
        async Task<CivicCase?> Existing()=>await db.CivicCases.AsNoTracking().SingleOrDefaultAsync(x=>x.ResidentId==resident&&x.Kind==kind&&x.SubmissionToken==input.SubmissionToken,ct);
        var old=await Existing();if(old!=null)return old.Fingerprint==fingerprint?old:throw new TransportProblem(409,"This submission token was used for different content. Start a new form.");
        if(await db.CivicCases.CountAsync(x=>x.ResidentId==resident&&x.CreatedAtUtc>now.AddHours(-1),ct)>=10)throw new TransportProblem(429,"Ten concerns were submitted in the last hour. Please wait before submitting another.");
        var row=new CivicCase{Kind=kind,ResidentId=resident,Subtype=input.Subtype,Description=input.Description.Trim(),Address=input.Address.Trim(),Latitude=input.Latitude.Value,Longitude=input.Longitude.Value,ObservedAtUtc=input.ObservedAtUtc.Value,ReportedDecibels=input.ReportedDecibels,Reference=input.Reference?.Trim(),SubmissionToken=input.SubmissionToken,Fingerprint=fingerprint,CreatedAtUtc=now};
        db.CivicCases.Add(row);
        try{await db.SaveChangesAsync(ct);}
        catch(DbUpdateException e)when(e.InnerException is Microsoft.Data.Sqlite.SqliteException{SqliteErrorCode:19})
        {db.ChangeTracker.Clear();old=await Existing();if(old?.Fingerprint==fingerprint)return old;throw new TransportProblem(409,"Concern changed or could not be recorded. Reload before retrying.");}
        catch(DbUpdateException e)when(e.InnerException is Microsoft.Data.Sqlite.SqliteException{SqliteErrorCode:5 or 6}){throw new TransportProblem(409,"Concern database busy. Reload before retrying.");}
        await transaction.CommitAsync(ct);
        return row;
    }
    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> Begin(CancellationToken ct)
    {try{return await db.Database.BeginTransactionAsync(ct);}catch(Microsoft.Data.Sqlite.SqliteException e)when(e.SqliteErrorCode is 5 or 6){throw new TransportProblem(409,"Concern database busy. Reload before retrying.");}}
    public async Task OfficialAsync(int actor,CancellationToken ct=default)
    {if(!await db.Users.AnyAsync(x=>x.Id==actor&&(x.Role=="Official"||x.Role=="CityOfficial"||x.Role=="Admin"),ct))throw new TransportProblem(403,"A provisioned city official is required.");}
    public async Task<CivicCaseDetail> DetailAsync(string rawKind,int id,int actor,bool official,CancellationToken ct=default)
    {
        var kind=Kind(rawKind);if(official)await OfficialAsync(actor,ct);
        var row=await db.CivicCases.AsNoTracking().SingleOrDefaultAsync(x=>x.Kind==kind&&x.Id==id&&(official||x.ResidentId==actor),ct)??throw new TransportProblem(404,"Concern not found.");
        var history=await db.CivicCaseReviews.AsNoTracking().Where(x=>x.CaseId==id).OrderBy(x=>x.AtUtc).ThenBy(x=>x.Id).Select(x=>new CivicReviewDisplay(x.State,x.Note,x.AtUtc)).ToListAsync(ct);return new(row,history);
    }
    public async Task ReviewAsync(string rawKind,int id,CivicReviewInput input,int actor,CancellationToken ct=default)
    {
        var kind=Kind(rawKind);Validate(input);await OfficialAsync(actor,ct);
        var row=await db.CivicCases.SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==kind,ct)??throw new TransportProblem(404,"Concern not found.");
        if(row.Revision!=input.ExpectedRevision)throw new TransportProblem(409,"Another reviewer changed this concern. Reload.");
        if(!(row.State=="Pending"&&input.State is "UnderReview" or "Rejected")&&!(row.State=="UnderReview"&&input.State is "Resolved" or "Rejected"))throw new TransportProblem(409,"This review transition is not available. Reload the concern.");
        if(string.IsNullOrWhiteSpace(input.Note)||input.Note.Trim().Length<10)throw new TransportProblem(400,"Explain the review outcome in at least ten characters.");
        row.State=input.State;row.Revision++;db.CivicCaseReviews.Add(new(){CaseId=id,OfficialId=actor,State=input.State,Note=input.Note.Trim(),AtUtc=clock.GetUtcNow().UtcDateTime});
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){throw new TransportProblem(409,"Another reviewer changed this concern. Reload.");}
        catch(DbUpdateException e)when(e.InnerException is Microsoft.Data.Sqlite.SqliteException{SqliteErrorCode:5 or 6}){throw new TransportProblem(409,"Concern database busy. Reload before retrying.");}
    }
}
