using System.ComponentModel.DataAnnotations;
namespace Nagorik.Api.Models;
public sealed class RiskAlertOptions
{
    public bool Enabled { get; set; } = true;
    [Range(0,1440)] public int CooldownMinutes { get; set; } = 60;
    [Range(1,10)] public int MaxAttempts { get; set; } = 3;
    [Range(1,1440)] public int RetryMinutes { get; set; } = 5;
    [Range(2,10)] public int LeaseMinutes { get; set; } = 2;
    [Range(1,60)] public int SendTimeoutSeconds { get; set; } = 30;
    [Range(1,1440)] public int MaxEventAgeMinutes { get; set; } = 60;
}
