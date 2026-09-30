using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nagorik.Api.Models;

public class ReportFormViewModel
{
    public string SubmissionToken { get; set; } = "";

    public List<SelectListItem> Categories { get; set; } = new();

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public ReportCategory Category { get; set; }

    public Authority Authority { get; set; }

    public string Location { get; set; } = "";

    public IFormFile? Image { get; set; }
}