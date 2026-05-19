using System.ComponentModel.DataAnnotations;

namespace HomeMonitoring.Shared.Models;

public class EmailSettings
{
    public const string SectionName = "Email";

    [Required]
    [MinLength(1)]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; }

    public bool UseSsl { get; set; }

    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string FromName { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string SmtpUsername { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string SmtpPassword { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string MonitoringEmail { get; set; } = string.Empty;

    [Range(1, 10_080)]
    public int DeviceOfflineThresholdMinutes { get; set; }
}
