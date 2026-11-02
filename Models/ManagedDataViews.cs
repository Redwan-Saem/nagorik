namespace Nagorik.Api.Models;
public sealed record ManagedZoneSummary(int Id,string Code,string Name,int GeoRevision,string? BoundarySource,bool? BoundaryIsSample,string? InputSource,bool? InputIsSample);
public sealed record ManagedChangeSummary(long Id,string Kind,int? ZoneId,string Source,bool? IsSample,string ActorUserId,DateTime AtUtc);
public sealed record ManagedDataView(int? EditingId,ZoneDataInput Input,IReadOnlyList<ManagedZoneSummary> Zones,IReadOnlyList<ManagedChangeSummary> Changes);
