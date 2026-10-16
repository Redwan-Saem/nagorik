using System.ComponentModel.DataAnnotations;
using Nagorik.Api.Models;

public class OfficialReportListQueryTests
{
    [Fact]
    public void DefaultsAndAllCategoryNamesAreValid()
    {
        var query = new OfficialReportListQuery();
        Assert.Equal(1, query.Page); Assert.Equal(20, OfficialReportListQuery.PageSize);
        Assert.Equal("date", query.Sort); Assert.Equal("desc", query.Direction);
        Assert.Null(query.AreaId); Assert.Empty(query.Categories);
        Assert.Empty(Validate(query));
        query.Categories = Enum.GetNames<ReportCategory>().Select(n => n.ToLowerInvariant()).ToArray();
        query.Page = int.MaxValue / OfficialReportListQuery.PageSize;
        Assert.Empty(Validate(query));
    }

    [Theory]
    [InlineData("numeric")]
    [InlineData("unknown")]
    [InlineData("too-many")]
    [InlineData("invalid-sort")]
    [InlineData("invalid-direction")]
    [InlineData("zero-page")]
    [InlineData("overflow-offset")]
    [InlineData("zero-area")]
    public void InvalidQueriesIdentifyTheRelevantField(string invalid)
    {
        var query = new OfficialReportListQuery();
        var field = invalid switch
        {
            "numeric" or "unknown" or "too-many" => nameof(query.Categories),
            "invalid-sort" => nameof(query.Sort), "invalid-direction" => nameof(query.Direction),
            "zero-area" => nameof(query.AreaId), _ => nameof(query.Page)
        };
        switch (invalid)
        {
            case "numeric": query.Categories = ["4"]; break;
            case "unknown": query.Categories = ["MissingCategory"]; break;
            case "too-many": query.Categories = Enumerable.Repeat("RoadDamage", 8).ToArray(); break;
            case "invalid-sort": query.Sort = "date;DROP TABLE Reports"; break;
            case "invalid-direction": query.Direction = "sideways"; break;
            case "zero-page": query.Page = 0; break;
            case "overflow-offset": query.Page = int.MaxValue / OfficialReportListQuery.PageSize + 1; break;
            case "zero-area": query.AreaId = 0; break;
        }
        Assert.Contains(Validate(query), error => error.MemberNames.Contains(field));
    }

    private static List<ValidationResult> Validate(OfficialReportListQuery query)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), errors, validateAllProperties: true);
        return errors;
    }
}
