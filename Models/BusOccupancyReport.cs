namespace Nagorik.Api.Models;
public sealed class BusOccupancyReport
{
    public int Id { get; set; }
    public int ResidentId { get; set; }
    public int LinkId { get; set; }
    public string Level { get; set; } = "SeatsAvailable";
    public DateTime ObservedAtUtc { get; set; }
}
