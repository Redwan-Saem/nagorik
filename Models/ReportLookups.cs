namespace Nagorik.Api.Models;

public sealed record ReportCategoryLookup(ReportCategory Category, string Name);

public static class ReportLookups
{
    // The persisted enum stays the single category source; these labels are for filter controls.
    public static IReadOnlyList<ReportCategoryLookup> Categories { get; } = Array.AsReadOnly(
        Enum.GetValues<ReportCategory>().Select(category => new ReportCategoryLookup(category, category switch
        {
            ReportCategory.WaterLogging => "Waterlogging",
            ReportCategory.RoadDamage => "Road damage",
            ReportCategory.PowerOutage => "Power outage",
            ReportCategory.UtilityAnomaly => "Utility Anomaly",
            _ => category.ToString()
        })).ToArray());
}
