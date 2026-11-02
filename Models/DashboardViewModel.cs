namespace Nagorik.Api.Models;
public sealed record DashboardCard(string Title,string Description,string Href);
public sealed record DashboardViewModel(bool Authenticated,string Role,IReadOnlyList<DashboardCard> Work,IReadOnlyList<DashboardCard> Public);
