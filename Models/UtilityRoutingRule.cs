namespace Nagorik.Api.Models;

public class UtilityRoutingRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public UtilityProvider Utility { get; set; }
    public AnomalyType? AnomalyType { get; set; }
    public int? AreaId { get; set; }
    public ReportArea? Area { get; set; }
    public double? MinLatitude { get; set; }
    public double? MaxLatitude { get; set; }
    public double? MinLongitude { get; set; }
    public double? MaxLongitude { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}
