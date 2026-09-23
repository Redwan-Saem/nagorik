namespace Nagorik.Api.Models
{
    public class ZoneMapData
    {
        public int Id { get; set; }
        public string ZoneId { get; set; } = "";
        public string BoundaryGeoJson { get; set; } = "";
        public string RiskLevel { get; set; } = "";
        public string DrainagePumpStatus { get; set; } = "";
    }
}