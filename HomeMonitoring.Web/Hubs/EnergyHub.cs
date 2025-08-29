using Microsoft.AspNetCore.SignalR;

namespace HomeMonitoring.Web.Hubs;

public class EnergyHub : Hub
{
    public async Task SendEnergyUpdate(int deviceId, double powerUsage)
    {
        await Clients.All.SendAsync("ReceiveEnergyUpdate", deviceId, powerUsage);
    }
}