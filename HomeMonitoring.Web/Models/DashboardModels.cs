using JetBrains.Annotations;

namespace HomeMonitoring.Web.Models;

public class DashboardData
{
    public List<DeviceCardData> Devices { get; set; } = [];
}

public class DeviceCardData
{
    public int DeviceId { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string ProductType { get; init; } = string.Empty;
    public double CurrentPowerW { get; init; }
    public double? TotalEnergyKWh { get; init; }
    public double? TotalGasM3 { get; init; }
    public DateTime LastUpdate { get; init; }
    public bool IsOnline { get; init; }
    public List<ChartDataPoint> ChartData { get; init; } = [];
}

public class ChartDataPoint
{
    public DateTime Timestamp { [UsedImplicitly] get; init; }
    public double PowerW { [UsedImplicitly] get; set; }
}