using System.Text.Json.Serialization;

namespace HomeMonitoring.Shared.Models.HomeWizard;

public record DeviceResponse
{
    [JsonPropertyName("product_name")] public required string ProductName { get; init; }

    [JsonPropertyName("product_type")] public required string ProductType { get; init; }

    [JsonPropertyName("serial")] public required string SerialNumber { get; init; }

    [JsonPropertyName("api_version")] public required string ApiVersion { get; init; }

    [JsonPropertyName("firmware_version")] public required string FirmwareVersion { get; init; }
}