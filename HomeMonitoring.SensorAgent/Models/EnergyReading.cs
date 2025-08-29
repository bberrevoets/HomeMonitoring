namespace HomeMonitoring.SensorAgent.Models;

public class EnergyReading
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public double ActivePowerW { get; set; }
    public double? TotalPowerImportT1KWh { get; set; }
    public double? TotalPowerImportT2KWh { get; set; }
    public double? TotalPowerExportT1KWh { get; set; }
    public double? TotalPowerExportT2KWh { get; set; }
    public double? TotalGasM3 { get; set; }

    // Navigation property
    public Device? Device { get; set; }
}