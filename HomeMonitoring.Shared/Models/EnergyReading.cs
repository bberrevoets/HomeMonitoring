namespace HomeMonitoring.Shared.Models;

public class EnergyReading
{
    public int Id { get; init; }
    public int DeviceId { get; init; }
    public DateTime Timestamp { get; init; }
    public double ActivePowerW { get; init; }
    public double? TotalPowerImportT1KWh { get; init; }
    public double? TotalPowerImportT2KWh { get; init; }
    public double? TotalPowerExportT1KWh { get; init; }
    public double? TotalPowerExportT2KWh { get; init; }
    public double? TotalGasM3 { get; init; }

    // Navigation property
    public Device? Device { get; init; }
}