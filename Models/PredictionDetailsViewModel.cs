namespace Nagorik.Api.Models;

public class PredictionDetailsViewModel
{
    public int Id { get; set; }

    public string ZoneName { get; set; } = "";

    public string RiskLevel { get; set; } = "";

    public string WindowText { get; set; } = "";

    public string IssuedAtText { get; set; } = "";
}