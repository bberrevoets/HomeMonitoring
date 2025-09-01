namespace HomeMonitoring.Web.Models;

public class DashboardData
{
    public List<DeviceCardData> Devices { get; set; } = new();
}

public class DeviceCardData
{
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public double CurrentPowerW { get; set; }
    public double? TotalEnergyKWh { get; set; }
    public double? TotalGasM3 { get; set; }
    public DateTime LastUpdate { get; set; }
    public bool IsOnline { get; set; }
    public List<ChartDataPoint> ChartData { get; set; } = new();
}

public class ChartDataPoint
{
    public DateTime Timestamp { get; set; }
    public double PowerW { get; set; }
}