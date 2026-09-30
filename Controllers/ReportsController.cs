using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers
{
    [Authorize(Roles = "Resident")]
    public class ReportsController : Controller
    {
        public IActionResult Create()
        {
            var vm = new ReportFormViewModel
            {
                Categories = Enum.GetValues<ReportCategory>()
                    .Select(c => new SelectListItem(c.ToString(), c.ToString()))
            };

            return View(vm);
        }
    }
}