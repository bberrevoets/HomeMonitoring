using HomeMonitoring.Web.Models;
using Microsoft.AspNetCore.SignalR;

namespace HomeMonitoring.Web.Hubs;

public class EnergyHub : Hub
{
    public async Task SendEnergyUpdate(int deviceId, double powerUsage)
    {
        await Clients.All.SendAsync("ReceiveEnergyUpdate", deviceId, powerUsage);
    }

    public async Task SendDashboardUpdate(DashboardData dashboardData)
    {
        await Clients.All.SendAsync("ReceiveDashboardUpdate", dashboardData);
    }

    public async Task SendDeviceChartUpdate(int deviceId, List<ChartDataPoint> chartData)
    {
        await Clients.All.SendAsync("ReceiveDeviceChartUpdate", deviceId, chartData);
    }

    public async Task JoinDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Dashboard");
    }

    public async Task LeaveDashboard()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Dashboard");
    }
}