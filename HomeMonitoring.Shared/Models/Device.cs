namespace HomeMonitoring.Shared.Models;

public class Device
{
    public int Id { get; init; }
    public required string Name { get; set; }
    public required string IpAddress { get; set; }
    public HomeWizardProductType ProductType { get; set; }
    public required string SerialNumber { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    public DateTime LastSeenAt { get; set; }
    public bool IsEnabled { get; set; } = true;

    // Store the original product type string for reference
    public string? ProductTypeRaw { get; set; }

    // Last-known device status, refreshed by the SensorAgent as it polls: WiFi every poll (free from
    // the energy response), firmware/API a few times a day. The Devices/Details page reads these from
    // the DB instead of contacting the device, which only accepts a single client connection.
    public string? FirmwareVersion { get; set; }
    public string? ApiVersion { get; set; }
    public string? WifiSsid { get; set; }
    public int? WifiStrength { get; set; }
    public DateTime? DeviceInfoUpdatedAt { get; set; }
}