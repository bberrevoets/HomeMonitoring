using System.ComponentModel.DataAnnotations;

namespace HomeMonitoring.Shared.Models.PhilipsHue;

public class HueLight
{
    public int Id { get; set; }
    
    [Required]
    public required string HueId { get; set; } // The ID from Hue Bridge
    
    [Required]
    public required string Name { get; set; }
    
    public string? ModelId { get; set; }
    
    public string? ManufacturerName { get; set; }
    
    public string? ProductName { get; set; }
    
    [Required]
    public required string BridgeIpAddress { get; set; }
    
    public DateTime DiscoveredAt { get; set; }
    
    public DateTime LastSeenAt { get; set; }
    
    public bool IsEnabled { get; set; } = true;
    
    // Navigation property
    public ICollection<HueLightReading> Readings { get; set; } = new List<HueLightReading>();
}