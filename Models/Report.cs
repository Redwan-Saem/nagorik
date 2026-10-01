namespace Nagorik.Api.Models;

public class Report
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public ReportCategory Category { get; set; }

    public Authority Authority { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public string Location { get; set; } = "";

    public string? ImageUrl { get; set; }


    // Location coordinates (T-022.5)
    public double Latitude { get; set; }

    public double Longitude { get; set; }


    // Duplicate detection (T-022.5)
    public bool IsPossibleDuplicate { get; set; }

    public int? DuplicateOfReportId { get; set; }


    // Crew assignment (T-004.1)
    public int? AssignedCrewId { get; set; }

    public Crew? AssignedCrew { get; set; }


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    // User relationship
    public int UserId { get; set; }

    public User? User { get; set; }
}