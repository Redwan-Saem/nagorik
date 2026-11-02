using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
public class ManagedDataTests
{
    internal const string Geometry="{\"type\":\"Polygon\",\"coordinates\":[[[90.37,23.74],[90.38,23.74],[90.38,23.75],[90.37,23.75],[90.37,23.74]]]}";
    [Fact]public void BoundariesRejectOpenCrossingOutsideAndInvalidHoles()
    {
        Assert.Contains("Polygon",GeoBoundaryValidator.Normalize(Geometry));
        foreach(var bad in new[]{Geometry.Replace("[90.37,23.74]]]","[90.375,23.74]]]"),"{\"type\":\"Polygon\",\"coordinates\":[[[90.37,23.74],[90.38,23.75],[90.37,23.75],[90.38,23.74],[90.37,23.74]]]}",Geometry.Replace("90.37","91.37"),"{}"})Assert.Throws<TransportProblem>(()=>GeoBoundaryValidator.Normalize(bad));
    }
    [Fact]public async Task GeographicEditsPreserveIdsAuditSourcesAndInvalidateOldRisk()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var s=new GeographicDataService(db,TimeProvider.System);
        var input=new ZoneDataInput{Code="G1",Name="Managed fixture",BoundaryGeoJson=Geometry,Source="Survey fixture — unverified",IsSample=true};var row=await s.SaveZoneAsync(null,input,"fixture-admin");Assert.Equal(1,row.GeoRevision);Assert.True(row.BoundaryIsSample);
        row.RiskLevel=ZoneRiskLevel.High;row.LastComputedAtUtc=DateTime.UtcNow;await db.SaveChangesAsync();
        await s.UpdateVulnerabilityAsync(row.Id,new(){ElevationMeters=1,DrainageDensityKmPerSqKm=2,HistoryScore=.3,Source="Input fixture",IsSample=true,ExpectedRevision=1},"fixture-admin");
        Assert.Equal(ZoneRiskLevel.Unknown,row.RiskLevel);Assert.Null(row.LastComputedAtUtc);Assert.Equal("Input fixture",row.InputSource);Assert.Equal(2,await db.ManagedDataChanges.CountAsync());
        Assert.Equal(409,(await Assert.ThrowsAsync<TransportProblem>(()=>s.SaveZoneAsync(row.Id,input,"fixture-admin"))).Status);
    }
    [Fact]public async Task CatalogImportPreservesIdsRejectsStaleRevisionAndAuditsConfiguration()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;await TransportSearchTests.Seed(db);var service=new TransportCatalogService(db,TimeProvider.System);var doc=await service.ExportAsync();doc.Source="Curated fixture catalog";
        Assert.Equal(1,await service.ImportAsync(doc,"fixture-admin"));Assert.Equal(4,await db.TransportLinks.CountAsync());Assert.Single(db.ManagedDataChanges);
        Assert.Equal(409,(await Assert.ThrowsAsync<TransportProblem>(()=>service.ImportAsync(doc,"fixture-admin"))).Status);
        var fresh=await service.ExportAsync();fresh.Source="Edited fixture catalog";fresh.Links[0].ToStopId=999;Assert.Equal(400,(await Assert.ThrowsAsync<TransportProblem>(()=>service.ImportAsync(fresh,"fixture-admin"))).Status);
    }
}
