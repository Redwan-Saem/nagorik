using System.ComponentModel.DataAnnotations;
namespace Nagorik.Api.Models;
public sealed class CivicCase
{
    public int Id{get;set;}public int ResidentId{get;set;}public string Kind{get;set;}="Noise";
    public string Subtype{get;set;}="Other";public string Description{get;set;}="";public string Address{get;set;}="";
    public double Latitude{get;set;}public double Longitude{get;set;}public DateTime ObservedAtUtc{get;set;}
    public double? ReportedDecibels{get;set;}public string? Reference{get;set;}
    public string SubmissionToken{get;set;}="";public string Fingerprint{get;set;}="";
    public string State{get;set;}="Pending";public int Revision{get;set;}=1;public DateTime CreatedAtUtc{get;set;}
}
public sealed class CivicCaseReview
{
    public int Id{get;set;}public int CaseId{get;set;}public int OfficialId{get;set;}
    public string State{get;set;}="UnderReview";public string Note{get;set;}="";public DateTime AtUtc{get;set;}
}
public sealed class CivicCaseInput
{
    [Required,StringLength(40)]public string Subtype{get;set;}="Other";
    [Required,StringLength(1500,MinimumLength=10)]public string Description{get;set;}="";
    [Required,StringLength(300)]public string Address{get;set;}="";
    [Required]public double? Latitude{get;set;}[Required]public double? Longitude{get;set;}
    [Required]public DateTime? ObservedAtUtc{get;set;}
    [Range(0,180)]public double? ReportedDecibels{get;set;}
    [StringLength(100)]public string? Reference{get;set;}
    [Required,StringLength(36,MinimumLength=36)]public string SubmissionToken{get;set;}="";
}
public sealed class CivicReviewInput
{
    [Range(1,int.MaxValue)]public int ExpectedRevision{get;set;}
    [Required]public string State{get;set;}="UnderReview";
    [Required,StringLength(1000,MinimumLength=10)]public string Note{get;set;}="";
}
public sealed record CivicReviewDisplay(string State,string Note,DateTime AtUtc);
public sealed record CivicCaseDetail(CivicCase Case,IReadOnlyList<CivicReviewDisplay> Reviews);
