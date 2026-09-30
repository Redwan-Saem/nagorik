using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nagorik.Api.Models
{
    public class ReportFormViewModel
    {
        public string SubmissionToken { get; set; } = Guid.NewGuid().ToString("N");

        public IEnumerable<SelectListItem> Categories { get; set; }
            = Enumerable.Empty<SelectListItem>();
    }
}