using System.Diagnostics;
using HomeMonitoring.SensorAgent.Metrics;
using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.SensorAgent;

public class Worker : BackgroundService
{
    // Named after the app so ServiceDefaults' AddSource("HomeMonitoring.*") captures these spans.
    private static readonly ActivitySource ActivitySource = new("HomeMonitoring.SensorAgent");

    private readonly ILogger<Worker> _logger;
    private readonly SensorAgentMetrics _metrics;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

    // Firmware/API version change rarely, so refresh them a few times a day rather than every poll.
    private readonly TimeSpan _deviceInfoRefreshInterval = TimeSpan.FromHours(6);
    private readonly IServiceProvider _serviceProvider;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, SensorAgentMetrics metrics)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SensorAgent starting at: {StartTime}", DateTimeOffset.Now);

        // Wait for database to be ready
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        // Main polling loop
        using var pollingTimer = new PeriodicTimer(_pollingInterval);

        while (await pollingTimer.WaitForNextTickAsync(stoppingToken)) await PollDevicesAsync(stoppingToken);
    }

    private async Task PollDevicesAsync(CancellationToken stoppingToken)
    {
        // Root span for the polling cycle; per-device reads, the HTTP calls (HttpClient
        // instrumentation) and the DB writes (SqlClient instrumentation) nest underneath.
        using var pollActivity = ActivitySource.StartActivity("PollDevices");

        List<Device> devices;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();

            // Detached snapshot of the enabled devices; each is polled below in its own scope.
            devices = await dbContext.Devices
                .AsNoTracking()
                .Where(d => d.IsEnabled)
                .ToListAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            pollActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error loading devices for polling");
            return;
        }

        pollActivity?.SetTag("devices.count", devices.Count);

        if (devices.Count == 0)
        {
            _logger.LogDebug("No devices configured yet");
            return;
        }

        _logger.LogDebug("Polling {DeviceCount} devices", devices.Count);

        // Poll every device concurrently so a single slow or unreachable device can't delay the others
        // (which previously starved healthy devices past the offline threshold). DbContext is not
        // thread-safe, so each poll runs in its own DI scope with its own context. Each task swallows
        // its own failures, so WhenAll never faults the polling loop.
        await Task.WhenAll(devices.Select(device => PollSingleDeviceAsync(device, stoppingToken)));
    }

    private async Task PollSingleDeviceAsync(Device device, CancellationToken stoppingToken)
    {
        using var deviceActivity = ActivitySource.StartActivity("PollDevice");
        deviceActivity?.SetTag("device.name", device.Name);
        deviceActivity?.SetTag("device.ip_address", device.IpAddress);
        deviceActivity?.SetTag("device.product_type", device.ProductType.ToString());

        // Skip unsupported devices (defensive — the query already filters to enabled devices).
        if (device.ProductType != HomeWizardProductType.HWE_P1 &&
            device.ProductType != HomeWizardProductType.HWE_SKT)
        {
            _logger.LogDebug("Skipping unsupported device type {ProductType} for device {DeviceName}",
                device.ProductType, device.Name);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
            var homeWizardService = scope.ServiceProvider.GetRequiredService<IHomeWizardService>();

            var energyData = await homeWizardService.GetEnergyDataAsync(
                device.IpAddress,
                device.ProductType,
                stoppingToken);

            var tracked = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == device.Id, stoppingToken);
            if (tracked is null) return; // Device was deleted mid-cycle.

            // Store the reading and mark the device as seen - it is responding.
            dbContext.EnergyReadings.Add(new EnergyReading
            {
                DeviceId = tracked.Id,
                Timestamp = DateTime.UtcNow,
                ActivePowerW = energyData.ActivePowerW ?? 0,
                TotalPowerImportT1KWh = energyData.TotalPowerImportT1KWh,
                TotalPowerImportT2KWh = energyData.TotalPowerImportT2KWh,
                TotalPowerExportT1KWh = energyData.TotalPowerExportT1KWh,
                TotalPowerExportT2KWh = energyData.TotalPowerExportT2KWh,
                TotalGasM3 = energyData.TotalGasM3
            });
            tracked.LastSeenAt = DateTime.UtcNow;

            // Persist last-known WiFi status (free — it's already in the energy response) so the
            // Devices/Details page can show it without contacting the connection-limited device.
            tracked.WifiSsid = energyData.WifiSsid;
            tracked.WifiStrength = energyData.WifiStrength;

            // Refresh firmware/API info a few times a day (or the first time we ever reach the device),
            // reusing this poll's keep-alive connection so we don't open a competing one.
            if (tracked.DeviceInfoUpdatedAt is null ||
                DateTime.UtcNow - tracked.DeviceInfoUpdatedAt.Value > _deviceInfoRefreshInterval)
            {
                try
                {
                    var info = await homeWizardService.GetDeviceInfoAsync(device.IpAddress, stoppingToken);
                    tracked.FirmwareVersion = info.FirmwareVersion;
                    tracked.ApiVersion = info.ApiVersion;
                    tracked.DeviceInfoUpdatedAt = DateTime.UtcNow;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    _logger.LogDebug(ex, "Could not refresh device info for {DeviceName} ({IpAddress})",
                        device.Name, device.IpAddress);
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);

            // Record successful metrics
            _metrics.IncrementSensorReadingsProcessed();
            _metrics.RecordProcessingTime(stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation(
                "Collected energy data from {DeviceName} ({ProductType}) at {IpAddress}: PowerUsage={PowerW}W",
                device.Name, device.ProductType, device.IpAddress, energyData.ActivePowerW);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down - not a device error.
        }
        catch (NotSupportedException ex)
        {
            _metrics.IncrementDeviceErrors();
            deviceActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning("Device {DeviceName} has unsupported product type: {Message}",
                device.Name, ex.Message);

            // Disable unsupported devices to avoid repeated errors.
            await DisableDeviceAsync(device.Id, device.Name, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _metrics.IncrementDeviceErrors();
            deviceActivity?.SetStatus(ActivityStatusCode.Error, "Device not responding (timeout)");
            // Expected when a device is offline/not responding - DeviceMonitoringService owns alerts.
            _logger.LogDebug("Device {DeviceName} ({ProductType}) at {IpAddress} is not responding (timeout)",
                device.Name, device.ProductType, device.IpAddress);
        }
        catch (HttpRequestException)
        {
            _metrics.IncrementDeviceErrors();
            deviceActivity?.SetStatus(ActivityStatusCode.Error, "Device not reachable");
            // Network errors are expected when a device is offline - DeviceMonitoringService owns alerts.
            _logger.LogDebug("Device {DeviceName} ({ProductType}) at {IpAddress} is not reachable",
                device.Name, device.ProductType, device.IpAddress);
        }
        catch (Exception ex)
        {
            _metrics.IncrementDeviceErrors();
            deviceActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Only log actual errors, not expected communication failures
            _logger.LogWarning(ex,
                "Unexpected error collecting data from device {DeviceName} ({ProductType}) at {IpAddress}",
                device.Name, device.ProductType, device.IpAddress);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task DisableDeviceAsync(int deviceId, string deviceName, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();

            var tracked = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, stoppingToken);
            if (tracked is null) return;

            tracked.IsEnabled = false;
            await dbContext.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disable unsupported device {DeviceName}", deviceName);
        }
    }
}
