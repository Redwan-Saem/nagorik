namespace Nagorik.Api.Models;

public class Crew
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int UserId { get; set; }

    public User? User { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
}
