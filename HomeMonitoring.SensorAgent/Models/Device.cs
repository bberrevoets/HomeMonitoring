namespace HomeMonitoring.SensorAgent.Models;

public class Device
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string IpAddress { get; set; }
    public required string ProductType { get; set; }
    public required string SerialNumber { get; set; }
    public required DateTime DiscoveredAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsEnabled { get; set; } = true;
}