namespace Nagorik.Api.Models;

public class CrewAssignment
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public Report? Report { get; set; }

    public int CrewId { get; set; }

    public Crew? Crew { get; set; }

    public int DispatcherId { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}