using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using HomeMonitoring.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomeMonitoring.Web.Services;

public class DashboardService : IDashboardService
{
    private readonly SensorDbContext _context;
    private readonly DashboardSettings _dashboardSettings;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        SensorDbContext context,
        ILogger<DashboardService> logger,
        IOptions<DashboardSettings> dashboardSettings)
    {
        _context = context;
        _logger = logger;
        _dashboardSettings = dashboardSettings.Value;
    }

    public async Task<DashboardData> GetDashboardDataAsync()
    {
        var devices = await _context.Devices
            .Where(d => d.IsEnabled)
            .ToListAsync();

        var dashboardData = new DashboardData();

        foreach (var device in devices)
        {
            var latestReading = await _context.EnergyReadings
                .Where(r => r.DeviceId == device.Id)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();

            var chartData = await GetDeviceChartDataAsync(device.Id, _dashboardSettings.ChartDataMinutes);

            // Use the same logic as the DeviceMonitoringService for consistency
            var timeSinceLastSeen = DateTime.UtcNow - device.LastSeenAt;
            var isOnline = timeSinceLastSeen.TotalMinutes <= _dashboardSettings.DeviceOfflineThresholdMinutes;

            _logger.LogDebug(
                "Device {DeviceName}: LastSeen={LastSeen}, TimeSince={TimeSince:F1} mins, IsOnline={IsOnline}",
                device.Name, device.LastSeenAt, timeSinceLastSeen.TotalMinutes, isOnline);

            var deviceCard = new DeviceCardData
            {
                DeviceId = device.Id,
                DeviceName = device.Name,
                ProductType = GetProductTypeDisplayName(device.ProductType),
                CurrentPowerW = latestReading?.ActivePowerW ?? 0,
                TotalEnergyKWh = GetTotalEnergyKWh(latestReading),
                TotalGasM3 = latestReading?.TotalGasM3,
                LastUpdate = latestReading?.Timestamp ?? device.LastSeenAt,
                IsOnline = isOnline,
                ChartData = chartData
            };

            dashboardData.Devices.Add(deviceCard);
        }

        return dashboardData;
    }

    public async Task<List<ChartDataPoint>> GetDeviceChartDataAsync(int deviceId, int minutes = 10)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-minutes);

        var readings = await _context.EnergyReadings
            .Where(r => r.DeviceId == deviceId && r.Timestamp >= cutoffTime)
            .OrderBy(r => r.Timestamp)
            .Select(r => new ChartDataPoint
            {
                Timestamp = r.Timestamp,
                PowerW = r.ActivePowerW
            })
            .ToListAsync();

        return readings;
    }

    private static string GetProductTypeDisplayName(HomeWizardProductType productType)
    {
        return productType switch
        {
            HomeWizardProductType.HWE_P1 => "P1 Smart Meter",
            HomeWizardProductType.HWE_SKT => "Energy Socket",
            HomeWizardProductType.HWE_WTR => "Water Meter",
            HomeWizardProductType.HWE_KWH1 => "kWh Meter (1-phase)",
            HomeWizardProductType.HWE_KWH3 => "kWh Meter (3-phase)",
            HomeWizardProductType.SDM230_wifi => "SDM230 Meter",
            HomeWizardProductType.SDM630_wifi => "SDM630 Meter",
            HomeWizardProductType.HWE_DSP => "Display",
            HomeWizardProductType.HWE_BAT => "Battery",
            _ => "Unknown Device"
        };
    }

    private static double? GetTotalEnergyKWh(EnergyReading? reading)
    {
        if (reading == null) return null;

        var t1 = reading.TotalPowerImportT1KWh ?? 0;
        var t2 = reading.TotalPowerImportT2KWh ?? 0;

        return t1 + t2 > 0 ? t1 + t2 : null;
    }
}