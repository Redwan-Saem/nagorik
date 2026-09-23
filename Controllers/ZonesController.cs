using Microsoft.AspNetCore.Mvc;

namespace Nagorik.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZonesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ZonesController(AppDbContext db) { _db = db; }

        [HttpGet("{zoneId}/risk")]
        public IActionResult GetRisk(string zoneId)
        {
            var zone = _db.WaterloggingRisks.FirstOrDefault(z => z.ZoneId == zoneId);
            if (zone == null) return NotFound(new { message = "No data for this zone" });
            return Ok(zone);
        }
    }
}