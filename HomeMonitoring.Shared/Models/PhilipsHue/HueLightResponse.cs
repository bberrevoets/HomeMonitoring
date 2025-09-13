using System.Text.Json.Serialization;

namespace HomeMonitoring.Shared.Models.PhilipsHue;

public class HueLightResponse
{
    [JsonPropertyName("state")]
    public HueLightState State { get; set; } = new();

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("modelid")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("manufacturername")]
    public string ManufacturerName { get; set; } = string.Empty;

    [JsonPropertyName("productname")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("uniqueid")]
    public string UniqueId { get; set; } = string.Empty;
}