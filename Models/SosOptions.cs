using System.ComponentModel.DataAnnotations;

namespace Nagorik.Api.Models;

public sealed class SosOptions
{
    public string[] ResponderEmails { get; set; } = [];
    [Range(1, 30)]
    public int NotificationTimeoutSeconds { get; set; } = 15;
}
