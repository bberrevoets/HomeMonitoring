using System.Text.Json.Serialization;

namespace HomeMonitoring.Shared.Models.PhilipsHue;

public class HueBridgeDiscoveryResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("internalipaddress")]
    public string InternalIpAddress { get; set; } = string.Empty;
}