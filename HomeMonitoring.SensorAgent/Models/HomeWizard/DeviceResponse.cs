namespace HomeMonitoring.SensorAgent.Models.HomeWizard;

public record DeviceResponse
{
    public required string ProductName { get; init; }
    public required string ProductType { get; init; }
    public required string SerialNumber { get; init; }
    public required string ApiVersion { get; init; }
    public required string FirmwareVersion { get; init; }
}