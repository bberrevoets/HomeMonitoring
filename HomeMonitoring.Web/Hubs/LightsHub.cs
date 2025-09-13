using Microsoft.AspNetCore.SignalR;

namespace HomeMonitoring.Web.Hubs
{
    public class LightsHub : Hub
    {
        public async Task JoinLightsPage()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "LightsPage");
        }

        public async Task LeaveLightsPage()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "LightsPage");
        }
    }
}