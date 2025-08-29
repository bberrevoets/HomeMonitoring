namespace HomeMonitoring.SensorAgent.Models.HomeWizard;

public record EnergyResponse
{
    public required string SmrVersion { get; init; }
    public string? MeterModel { get; init; }
    public required string WifiSsid { get; init; }
    public required int WifiStrength { get; init; }
    public double? TotalPowerImportT1KWh { get; init; }
    public double? TotalPowerImportT2KWh { get; init; }
    public double? TotalPowerExportT1KWh { get; init; }
    public double? TotalPowerExportT2KWh { get; init; }
    public required double ActivePowerW { get; init; }
    public required double ActivePowerL1W { get; init; }
    public double? ActivePowerL2W { get; init; }
    public double? ActivePowerL3W { get; init; }
    public required double TotalGasM3 { get; init; }
    public required string GasTimestamp { get; init; }
}