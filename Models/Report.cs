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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    // User relationship
    public int UserId { get; set; }

    public User? User { get; set; }
}