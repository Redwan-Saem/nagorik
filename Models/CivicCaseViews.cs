namespace Nagorik.Api.Models;
public sealed record CivicSubmitView(string Kind,CivicCaseInput Input,IReadOnlyList<CivicCase> Items,string ObservedLocal);
public sealed record CivicQueueView(string Kind,IReadOnlyList<CivicCase> Items);
public sealed record CivicDetailView(CivicCaseDetail Detail,bool Official);
