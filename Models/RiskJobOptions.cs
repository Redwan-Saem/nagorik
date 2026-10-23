using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public sealed class RiskJobOptions
{
    public bool Enabled { get; set; } = true;
    [Range(1, 1440)] public int PollMinutes { get; set; } = 5;
}
