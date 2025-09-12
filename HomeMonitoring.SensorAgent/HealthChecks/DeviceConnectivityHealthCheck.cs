using HomeMonitoring.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HomeMonitoring.SensorAgent.HealthChecks;

public class DeviceConnectivityHealthCheck : IHealthCheck
{
    private readonly ILogger<DeviceConnectivityHealthCheck> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DeviceConnectivityHealthCheck(IServiceProvider serviceProvider,
        ILogger<DeviceConnectivityHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();

            var devices = await dbContext.Devices.ToListAsync(cancellationToken);
            var totalDevices = devices.Count;

            if (totalDevices == 0) return HealthCheckResult.Healthy("No devices configured");

            var offlineThreshold = TimeSpan.FromMinutes(5); // Configurable threshold
            var onlineDevices = devices.Count(d => d.LastSeenAt > DateTime.UtcNow - offlineThreshold);
            var offlineDevices = totalDevices - onlineDevices;

            var healthData = new Dictionary<string, object>
            {
                ["total_devices"] = totalDevices,
                ["online_devices"] = onlineDevices,
                ["offline_devices"] = offlineDevices,
                ["offline_threshold_minutes"] = offlineThreshold.TotalMinutes
            };

            if (offlineDevices == 0)
                return HealthCheckResult.Healthy($"All {totalDevices} devices are online", healthData);

            if (offlineDevices < totalDevices * 0.5) // More than 50% online
                return HealthCheckResult.Degraded($"{offlineDevices} of {totalDevices} devices are offline",
                    null, healthData);

            return HealthCheckResult.Unhealthy($"{offlineDevices} of {totalDevices} devices are offline",
                data: healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking device connectivity health");
            return HealthCheckResult.Unhealthy("Failed to check device connectivity", ex);
        }
    }
}