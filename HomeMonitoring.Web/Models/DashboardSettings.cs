namespace HomeMonitoring.Web.Models;

public class DashboardSettings
{
    public const string SectionName = "Dashboard";
    
    /// <summary>
    /// How often to send updates to clients (in seconds). Default is 30 seconds.
    /// </summary>
    public int UpdateIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// How many minutes since last seen before a device is considered offline. Default is 5 minutes.
    /// </summary>
    public int DeviceOfflineThresholdMinutes { get; set; } = 5;
    
    /// <summary>
    /// How many minutes of chart data to show. Default is 10 minutes.
    /// </summary>
    public int ChartDataMinutes { get; set; } = 10;
}