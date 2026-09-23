namespace Nagorik.Api.Models
{
    public class WaterloggingRisk
    {
        public int Id { get; set; }
        public string ZoneId { get; set; } = "";
        public string ZoneName { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string RiskLevel { get; set; } = "";
        public DateTime LastUpdated { get; set; }
    }
}