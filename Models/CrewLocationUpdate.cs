using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;
public sealed class CrewLocationUpdate
{
    [Required, Range(-90, 90)] public double? Latitude { get; set; }
    [Required, Range(-180, 180)] public double? Longitude { get; set; }
}
