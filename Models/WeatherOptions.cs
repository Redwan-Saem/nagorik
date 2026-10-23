using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public sealed class WeatherOptions
{
    public bool Enabled { get; set; } = true;
    [Range(15, 1440)] public int PollMinutes { get; set; } = 60;
    [Range(1, 60)] public int RequestTimeoutSeconds { get; set; } = 15;
    [Range(0, 3)] public int MaxRetries { get; set; } = 2;
    [Range(0, 5000)] public int RetryDelayMilliseconds { get; set; } = 1000;
    [Range(15, 1440)] public int MaxAgeMinutes { get; set; } = 90;
}
