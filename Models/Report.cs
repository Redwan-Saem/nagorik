namespace Nagorik.Api.Models;

public class Report
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public ReportCategory Category { get; set; }

    public Authority Authority { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public int? AreaId { get; set; }
    public ReportArea? Area { get; set; }
    public ReportUrgency Urgency { get; set; } = ReportUrgency.Normal;

    public string Location { get; set; } = "";

    public string? ImageUrl { get; set; }

    // Utility anomaly metadata is optional for existing/general reports (T-009.1).
    public AnomalyType? AnomalyType { get; set; }
    public UtilityProvider? Utility { get; set; }
    public string? ReferenceNo { get; set; }
    public List<string> PhotoPaths { get; set; } = [];


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
