using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Nagorik.Api.Models;

public class CreateReportRequest
{
    [Required]
    public ReportCategory? Category { get; set; }

    [Required, StringLength(500)]
    public string Description { get; set; } = "";

    [Required]
    public double? Latitude { get; set; }

    [Required]
    public double? Longitude { get; set; }

    [Required, StringLength(300)]
    public string AddressText { get; set; } = "";

    [Required, StringLength(64)]
    public string SubmissionToken { get; set; } = "";

    [Required]
    public IFormFile? Photo { get; set; }
}