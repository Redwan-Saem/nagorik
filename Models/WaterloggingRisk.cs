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


        // T-002.1 Notification fields
        public DateTime? ExpectedStartUtc { get; set; }

        public DateTime? ExpectedEndUtc { get; set; }

        public DateTime? AlertProcessedAtUtc { get; set; }
    }
}