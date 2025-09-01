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
    private bool _isFirstCheck = true;

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

        // Do an initial check
        await CheckDeviceStatusesAsync(stoppingToken);

        // Then continue with periodic checks
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

            _logger.LogDebug("Checking status of {DeviceCount} devices with threshold {Threshold} minutes",
                devices.Count, _emailSettings.DeviceOfflineThresholdMinutes);

            foreach (var device in devices)
            {
                var timeSinceLastSeen = now - device.LastSeenAt;
                var isCurrentlyOffline = timeSinceLastSeen > threshold;

                // Check if this is a new device we haven't seen before
                var isNewDevice = !_deviceStatuses.ContainsKey(device.Id);

                // Get or create device status
                var status = _deviceStatuses.GetOrAdd(device.Id, new DeviceStatus
                {
                    DeviceId = device.Id,
                    IsOnline = true // Always start with online assumption for new devices
                });

                _logger.LogDebug(
                    "Device {DeviceName}: LastSeen={LastSeen}, TimeSince={TimeSince}, IsOffline={IsOffline}, StatusOnline={StatusOnline}, IsNew={IsNew}",
                    device.Name, device.LastSeenAt, timeSinceLastSeen, isCurrentlyOffline, status.IsOnline,
                    isNewDevice);

                if (isCurrentlyOffline && status.IsOnline)
                {
                    // Device just went offline (or we just discovered it's offline)
                    _logger.LogWarning("Device {DeviceName} has gone offline. Last seen: {LastSeen} ({TimeSince} ago)",
                        device.Name, device.LastSeenAt, timeSinceLastSeen);

                    status.IsOnline = false;
                    status.WentOfflineAt = device.LastSeenAt;

                    try
                    {
                        // Send offline alert
                        await emailService.SendDeviceOfflineAlertAsync(
                            device.Name,
                            device.ProductType.ToString(),
                            device.IpAddress,
                            device.LastSeenAt,
                            cancellationToken);

                        status.LastOfflineAlertSent = now;
                        _logger.LogInformation("Sent offline alert for device {DeviceName}", device.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send offline alert for device {DeviceName}", device.Name);
                    }
                }
                else if (!isCurrentlyOffline && !status.IsOnline)
                {
                    // Device came back online
                    _logger.LogInformation("Device {DeviceName} is back online", device.Name);

                    var offlineSince = status.WentOfflineAt ?? device.LastSeenAt;

                    try
                    {
                        // Send back online alert
                        await emailService.SendDeviceBackOnlineAlertAsync(
                            device.Name,
                            device.ProductType.ToString(),
                            device.IpAddress,
                            offlineSince,
                            cancellationToken);

                        _logger.LogInformation("Sent back online alert for device {DeviceName}", device.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send back online alert for device {DeviceName}", device.Name);
                    }

                    status.IsOnline = true;
                    status.WentOfflineAt = null;
                    status.LastOfflineAlertSent = null;
                }
                else if (isCurrentlyOffline && !status.IsOnline)
                {
                    // Device is still offline
                    if (isNewDevice && _isFirstCheck)
                    {
                        // This is a device that was already offline when we started
                        // Send an initial alert
                        _logger.LogWarning(
                            "Device {DeviceName} was already offline at startup. Last seen: {LastSeen} ({TimeSince} ago)",
                            device.Name, device.LastSeenAt, timeSinceLastSeen);

                        try
                        {
                            await emailService.SendDeviceOfflineAlertAsync(
                                device.Name,
                                device.ProductType.ToString(),
                                device.IpAddress,
                                device.LastSeenAt,
                                cancellationToken);

                            status.LastOfflineAlertSent = now;
                            status.WentOfflineAt = device.LastSeenAt;
                            _logger.LogInformation("Sent initial offline alert for device {DeviceName}", device.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send initial offline alert for device {DeviceName}",
                                device.Name);
                        }
                    }
                    else if (status.LastOfflineAlertSent.HasValue &&
                             now - status.LastOfflineAlertSent.Value > TimeSpan.FromHours(24))
                    {
                        // Send reminder every 24 hours
                        _logger.LogWarning("Device {DeviceName} is still offline (24h reminder)", device.Name);

                        try
                        {
                            await emailService.SendDeviceOfflineAlertAsync(
                                device.Name,
                                device.ProductType.ToString(),
                                device.IpAddress,
                                device.LastSeenAt,
                                cancellationToken);

                            status.LastOfflineAlertSent = now;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send reminder alert for device {DeviceName}", device.Name);
                        }
                    }
                }
                else if (!isCurrentlyOffline && status.IsOnline)
                {
                    _logger.LogDebug("Device {DeviceName} is online and working normally", device.Name);
                }
            }

            // After first check, set flag to false
            _isFirstCheck = false;

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