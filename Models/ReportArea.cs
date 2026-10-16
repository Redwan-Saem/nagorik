namespace Nagorik.Api.Models;

public class ReportArea
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public enum ReportUrgency
{
    Low,
    Normal,
    High,
    Critical
}
