namespace HomeMonitoring.Shared.Models;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 1025; // Mailpit default port
    public bool UseSsl { get; set; } = false;
    public string FromEmail { get; set; } = "homemonitoring@localhost";
    public string FromName { get; set; } = "Home Monitoring System";
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }

    // Monitoring settings
    public string MonitoringEmail { get; set; } = "admin@example.com"; // Hardcoded for now
    public int DeviceOfflineThresholdMinutes { get; set; } = 30; // Alert after 30 minutes offline
}