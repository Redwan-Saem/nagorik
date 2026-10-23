namespace Nagorik.Api.Models;
public sealed record ServiceZone(int Id,string Name);
public sealed record MunicipalPublicView(string Kind,IReadOnlyList<ServiceZone> Zones,int? SelectedZone,MunicipalWeek? Week);
public sealed record MunicipalManageView(string Kind,IReadOnlyList<ServiceZone> Zones,IReadOnlyList<MunicipalSchedule> Items,int? EditingId,MunicipalScheduleInput Input);
