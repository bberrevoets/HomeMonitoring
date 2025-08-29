using System.Collections.Concurrent;
using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomeMonitoring.SensorAgent.Services;

public class DeviceMonitoringService : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes
    private readonly ConcurrentDictionary<int, DeviceStatus> _deviceStatuses = new();
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<DeviceMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DeviceMonitoringService(
        IServiceProvider serviceProvider,
        IOptions<EmailSettings> emailSettings,
        ILogger<DeviceMonitoringService> logger)
    {
        _serviceProvider = serviceProvider;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Device Monitoring Service starting");

        // Wait a bit for everything to initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using var timer = new PeriodicTimer(_checkInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken)) await CheckDeviceStatusesAsync(stoppingToken);
    }

    private async Task CheckDeviceStatusesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var devices = await dbContext.Devices
                .Where(d => d.IsEnabled)
                .ToListAsync(cancellationToken);

            var threshold = TimeSpan.FromMinutes(_emailSettings.DeviceOfflineThresholdMinutes);
            var now = DateTime.UtcNow;

            foreach (var device in devices)
            {
                var timeSinceLastSeen = now - device.LastSeenAt;
                var isCurrentlyOffline = timeSinceLastSeen > threshold;

                // Get or create device status
                var status = _deviceStatuses.GetOrAdd(device.Id, new DeviceStatus
                {
                    DeviceId = device.Id,
                    IsOnline = !isCurrentlyOffline
                });

                if (isCurrentlyOffline && status.IsOnline)
                {
                    // Device just went offline
                    _logger.LogWarning("Device {DeviceName} has gone offline. Last seen: {LastSeen}",
                        device.Name, device.LastSeenAt);

                    status.IsOnline = false;
                    status.WentOfflineAt = device.LastSeenAt;

                    // Send offline alert
                    await emailService.SendDeviceOfflineAlertAsync(
                        device.Name,
                        device.ProductType.ToString(),
                        device.IpAddress,
                        device.LastSeenAt,
                        cancellationToken);

                    status.LastOfflineAlertSent = now;
                }
                else if (!isCurrentlyOffline && !status.IsOnline)
                {
                    // Device came back online
                    _logger.LogInformation("Device {DeviceName} is back online", device.Name);

                    var offlineSince = status.WentOfflineAt ?? device.LastSeenAt;

                    // Send back online alert
                    await emailService.SendDeviceBackOnlineAlertAsync(
                        device.Name,
                        device.ProductType.ToString(),
                        device.IpAddress,
                        offlineSince,
                        cancellationToken);

                    status.IsOnline = true;
                    status.WentOfflineAt = null;
                    status.LastOfflineAlertSent = null;
                }
                else if (isCurrentlyOffline && !status.IsOnline)
                {
                    // Device is still offline - check if we should send a reminder
                    // Send reminder every 24 hours
                    if (status.LastOfflineAlertSent.HasValue &&
                        now - status.LastOfflineAlertSent.Value > TimeSpan.FromHours(24))
                    {
                        _logger.LogWarning("Device {DeviceName} is still offline (24h reminder)", device.Name);

                        await emailService.SendDeviceOfflineAlertAsync(
                            device.Name,
                            device.ProductType.ToString(),
                            device.IpAddress,
                            device.LastSeenAt,
                            cancellationToken);

                        status.LastOfflineAlertSent = now;
                    }
                }
            }

            // Clean up statuses for devices that no longer exist
            var deviceIds = devices.Select(d => d.Id).ToHashSet();
            var toRemove = _deviceStatuses.Keys.Where(id => !deviceIds.Contains(id)).ToList();
            foreach (var id in toRemove) _deviceStatuses.TryRemove(id, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking device statuses");
        }
    }
}