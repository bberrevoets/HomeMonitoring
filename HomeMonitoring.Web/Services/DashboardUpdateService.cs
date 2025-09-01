using HomeMonitoring.Web.Hubs;
using HomeMonitoring.Web.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace HomeMonitoring.Web.Services;

public class DashboardUpdateService : BackgroundService
{
    private readonly DashboardSettings _dashboardSettings;
    private readonly ILogger<DashboardUpdateService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DashboardUpdateService(
        IServiceProvider serviceProvider,
        ILogger<DashboardUpdateService> logger,
        IOptions<DashboardSettings> dashboardSettings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dashboardSettings = dashboardSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var updateInterval = TimeSpan.FromSeconds(_dashboardSettings.UpdateIntervalSeconds);

        _logger.LogInformation(
            "Dashboard update service starting with {UpdateInterval} second intervals",
            _dashboardSettings.UpdateIntervalSeconds);

        // Add a small delay to allow the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(updateInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            try
            {
                _logger.LogInformation("Starting dashboard update cycle");
                await SendDashboardUpdatesAsync(stoppingToken);
                _logger.LogInformation("Dashboard update cycle completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending dashboard updates");
            }
    }

    private async Task SendDashboardUpdatesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<EnergyHub>>();

        _logger.LogDebug("Retrieving dashboard data");
        var dashboardData = await dashboardService.GetDashboardDataAsync();

        _logger.LogDebug("Sending dashboard update to Dashboard group with {DeviceCount} devices",
            dashboardData.Devices.Count);

        await hubContext.Clients.Group("Dashboard")
            .SendAsync("ReceiveDashboardUpdate", dashboardData, cancellationToken);

        _logger.LogInformation(
            "Sent dashboard update to Dashboard group - {DeviceCount} devices, {OnlineCount} online",
            dashboardData.Devices.Count,
            dashboardData.Devices.Count(d => d.IsOnline));
    }
}