namespace Nagorik.Api.Models;

public sealed record OfficialReportFiltersViewModel(IReadOnlyList<ReportArea> Areas)
{
    public IReadOnlyList<ReportCategoryLookup> Categories => ReportLookups.Categories;
}
