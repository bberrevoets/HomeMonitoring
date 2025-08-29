namespace HomeMonitoring.SensorAgent.Models;

public class Device
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string IpAddress { get; set; }
    public HomeWizardProductType ProductType { get; set; }
    public required string SerialNumber { get; set; }
    public required DateTime DiscoveredAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsEnabled { get; set; } = true;

    // Store the original product type string for reference
    public string? ProductTypeRaw { get; set; }
}