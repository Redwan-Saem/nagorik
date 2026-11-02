using System.ComponentModel.DataAnnotations;
namespace Nagorik.Api.Models;
public sealed class MunicipalSchedule
{
    public int Id{get;set;}
    public string Kind{get;set;}="Water";
    public int ZoneId{get;set;}
    public DateOnly FromDate{get;set;}
    public DateOnly ToDate{get;set;}
    public int DaysMask{get;set;}
    public int StartMinute{get;set;}
    public int EndMinute{get;set;}
    public string Source{get;set;}="";
    public string Notes{get;set;}="";
    public string State{get;set;}="Draft";
    public int Revision{get;set;}=1;
    public int UpdatedByUserId{get;set;}
    public DateTime UpdatedAtUtc{get;set;}
    public DateTime? PublishedAtUtc{get;set;}
}
public sealed class MunicipalScheduleInput
{
    [Range(1,int.MaxValue)]public int ZoneId{get;set;}
    [Required]public DateOnly? FromDate{get;set;}
    [Required]public DateOnly? ToDate{get;set;}
    [Range(1,127)]public int DaysMask{get;set;}=127;
    [Range(0,1439)]public int StartMinute{get;set;}=480;
    [Range(1,1440)]public int EndMinute{get;set;}=600;
    [Required,StringLength(300)]public string Source{get;set;}="";
    [StringLength(1000)]public string Notes{get;set;}="";
    public int ExpectedRevision{get;set;}
}
public sealed record MunicipalWindow(int StartMinute,int EndMinute,string Source,string Notes,DateTime PublishedAtUtc);
public sealed record MunicipalDay(DateOnly Date,bool IsPublished,IReadOnlyList<MunicipalWindow> Windows);
public sealed record MunicipalWeek(string Kind,int ZoneId,string ZoneName,string TimeZone,IReadOnlyList<MunicipalDay> Days);
