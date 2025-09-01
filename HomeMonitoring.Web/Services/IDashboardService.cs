using HomeMonitoring.Web.Models;

namespace HomeMonitoring.Web.Services;

public interface IDashboardService
{
    Task<DashboardData> GetDashboardDataAsync();
    Task<List<ChartDataPoint>> GetDeviceChartDataAsync(int deviceId, int minutes = 10);
}