using System.ComponentModel.DataAnnotations;
namespace Nagorik.Api.Models;
public sealed class ManagedDataChange
{
    public long Id{get;set;}public int? ZoneId{get;set;}public string Kind{get;set;}="Zone";
    public string ActorUserId{get;set;}="";public DateTime AtUtc{get;set;}public string Source{get;set;}="";
    public bool? IsSample{get;set;}public string PayloadJson{get;set;}="{}";
}
public sealed class TransportCatalogState{public int Id{get;set;}=1;public int Revision{get;set;}}
public sealed class ZoneDataInput
{
    [Required,RegularExpression("^[A-Z][A-Z0-9_-]{0,39}$")]public string Code{get;set;}="";
    [Required,StringLength(100)]public string Name{get;set;}="";
    [Required,StringLength(100000)]public string BoundaryGeoJson{get;set;}="";
    [Range(-500,9000)]public double? ElevationMeters{get;set;}
    [Range(0,100)]public double? DrainageDensityKmPerSqKm{get;set;}
    [Range(0,1)]public double? HistoryScore{get;set;}
    [Required,StringLength(300)]public string Source{get;set;}="";
    public bool IsSample{get;set;}
    [Range(0,int.MaxValue)]public int ExpectedRevision{get;set;}
}
public sealed class TransportCatalogDocument
{
    public int ExpectedRevision{get;set;}public string Source{get;set;}="";
    public List<TransportStop> Stops{get;set;}=[];public List<TransportLink> Links{get;set;}=[];public List<TransportFareRule> FareRules{get;set;}=[];
}
