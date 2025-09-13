using System.Text.Json.Serialization;

namespace HomeMonitoring.Shared.Models.PhilipsHue;

public class HueLightState
{
    [JsonPropertyName("on")]
    public bool On { get; set; }

    [JsonPropertyName("bri")]
    public byte? Brightness { get; set; }

    [JsonPropertyName("hue")]
    public int? Hue { get; set; }

    [JsonPropertyName("sat")]
    public byte? Saturation { get; set; }

    [JsonPropertyName("ct")]
    public int? ColorTemperature { get; set; }

    [JsonPropertyName("reachable")]
    public bool Reachable { get; set; }
}