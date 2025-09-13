namespace HomeMonitoring.Shared.Models.PhilipsHue;

public class HueLightReading
{
    public int Id { get; set; }
    
    public int HueLightId { get; set; }
    
    public DateTime Timestamp { get; set; }
    
    public bool On { get; set; }
    
    public byte Brightness { get; set; } // 0-254
    
    public int? Hue { get; set; } // 0-65535
    
    public byte? Saturation { get; set; } // 0-254
    
    public int? ColorTemperature { get; set; } // in Kelvin
    
    public bool Reachable { get; set; }
    
    // Navigation property
    public HueLight? HueLight { get; set; }
}