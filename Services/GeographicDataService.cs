using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
namespace Nagorik.Api.Services;
public sealed class GeographicDataService(AppDbContext db,TimeProvider clock)
{
    private void Audit(Zone row,string kind,string source,bool? sample,string actor)=>db.ManagedDataChanges.Add(new(){ZoneId=row.Id,Kind=kind,Source=source,IsSample=sample,ActorUserId=actor,AtUtc=clock.GetUtcNow().UtcDateTime,PayloadJson=JsonSerializer.Serialize(new{row.Code,row.Name,row.BoundaryGeoJson,row.BoundarySource,row.BoundaryIsSample,row.ElevationMeters,row.DrainageDensityKmPerSqKm,row.HistoryScore,row.InputSource,row.InputIsSample,row.GeoRevision})});
    private static void Validate(object input){var errors=new List<ValidationResult>();if(!Validator.TryValidateObject(input,new(input),errors,true))throw new TransportProblem(400,string.Join(" ",errors.Select(x=>x.ErrorMessage)));}
    private static void Invalidate(Zone row){row.RiskLevel=ZoneRiskLevel.Unknown;row.LastComputedAtUtc=null;row.RiskFactorsJson=null;}
    private async Task Save(CancellationToken ct)
    {
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){throw new TransportProblem(409,"Geographic data changed. Reload before saving.");}
        catch(DbUpdateException e)when(e.InnerException is Microsoft.Data.Sqlite.SqliteException{SqliteErrorCode:19}){throw new TransportProblem(409,"Zone code already exists or data constraints failed. Reload and check the input.");}
    }
    public async Task<Zone> SaveZoneAsync(int? id,ZoneDataInput input,string actor,CancellationToken ct=default)
    {
        Validate(input);if(string.IsNullOrWhiteSpace(input.Source)||string.IsNullOrWhiteSpace(input.Name))throw new TransportProblem(400,"Name and data source are required.");
        var geometry=GeoBoundaryValidator.Normalize(input.BoundaryGeoJson);
        if(new[]{input.ElevationMeters,input.DrainageDensityKmPerSqKm,input.HistoryScore}.Any(x=>x.HasValue&&!double.IsFinite(x.Value)))throw new TransportProblem(400,"Vulnerability values must be finite or empty.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var row=id.HasValue?await db.Zones.SingleOrDefaultAsync(x=>x.Id==id,ct):new Zone{Code=input.Code};if(row==null)throw new TransportProblem(404,"Zone not found.");
        if(row.GeoRevision!=input.ExpectedRevision)throw new TransportProblem(409,"Geographic data changed. Reload before saving.");
        if(id.HasValue&&!string.Equals(row.Code,input.Code,StringComparison.OrdinalIgnoreCase))throw new TransportProblem(400,"Stable zone codes cannot be renamed. Create a separately reviewed zone instead.");
        var changedBoundary=id.HasValue&&row.BoundaryGeoJson!=geometry;
        row.Name=input.Name.Trim();row.BoundaryGeoJson=geometry;row.BoundarySource=input.Source.Trim();row.BoundaryIsSample=input.IsSample;row.InputSource=input.Source.Trim();row.InputIsSample=input.IsSample;
        row.ElevationMeters=input.ElevationMeters;row.DrainageDensityKmPerSqKm=input.DrainageDensityKmPerSqKm;row.HistoryScore=input.HistoryScore;row.GeoRevision++;row.GeoUpdatedAtUtc=clock.GetUtcNow().UtcDateTime;Invalidate(row);
        if(!id.HasValue)db.Zones.Add(row);await Save(ct);
        if(changedBoundary)await db.RainfallReadings.Where(x=>x.ZoneId==row.Id).ExecuteDeleteAsync(ct);
        Audit(row,"Zone",input.Source.Trim(),input.IsSample,actor);await Save(ct);await tx.CommitAsync(ct);return row;
    }
    public async Task UpdateVulnerabilityAsync(int id,ZoneVulnerabilityInput input,string actor,CancellationToken ct=default)
    {
        Validate(input);if(new[]{input.ElevationMeters,input.DrainageDensityKmPerSqKm,input.HistoryScore}.Any(x=>x.HasValue&&!double.IsFinite(x.Value)))throw new TransportProblem(400,"Use finite vulnerability values.");
        var row=await db.Zones.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new TransportProblem(404,"Zone not found.");
        if(input.ExpectedRevision.HasValue&&row.GeoRevision!=input.ExpectedRevision)throw new TransportProblem(409,"Zone inputs changed. Reload before saving.");
        row.ElevationMeters=input.ElevationMeters;row.DrainageDensityKmPerSqKm=input.DrainageDensityKmPerSqKm;row.HistoryScore=input.HistoryScore;
        row.InputSource=string.IsNullOrWhiteSpace(input.Source)?"Legacy manual input; source not supplied or verified":input.Source.Trim();row.InputIsSample=input.IsSample;row.GeoRevision++;row.GeoUpdatedAtUtc=clock.GetUtcNow().UtcDateTime;Invalidate(row);
        Audit(row,"Vulnerability",row.InputSource,row.InputIsSample,actor);await Save(ct);
    }
}
