namespace HomeMonitoring.Shared.Models;

public class Device
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string IpAddress { get; set; }
    public HomeWizardProductType ProductType { get; set; }
    public required string SerialNumber { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public DateTime LastSeenAt { get; set; }
    public bool IsEnabled { get; set; } = true;

    // Store the original product type string for reference
    public string? ProductTypeRaw { get; set; }
}