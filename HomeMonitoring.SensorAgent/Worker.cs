using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.SensorAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);
    private readonly IServiceProvider _serviceProvider;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
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
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
            var homeWizardService = scope.ServiceProvider.GetRequiredService<IHomeWizardService>();

            // Get all enabled devices
            var devices = await dbContext.Devices
                .Where(d => d.IsEnabled)
                .ToListAsync(stoppingToken);

            if (devices.Count == 0)
            {
                _logger.LogDebug("No devices configured yet");
                return;
            }

            _logger.LogDebug("Polling {DeviceCount} devices", devices.Count);

            foreach (var device in devices)
                try
                {
                    // Skip unsupported devices
                    if (device.ProductType != HomeWizardProductType.HWE_P1 &&
                        device.ProductType != HomeWizardProductType.HWE_SKT)
                    {
                        _logger.LogDebug("Skipping unsupported device type {ProductType} for device {DeviceName}",
                            device.ProductType, device.Name);
                        continue;
                    }

                    var energyData = await homeWizardService.GetEnergyDataAsync(
                        device.IpAddress,
                        device.ProductType,
                        stoppingToken);

                    // Store the reading
                    var reading = new EnergyReading
                    {
                        DeviceId = device.Id,
                        Timestamp = DateTime.UtcNow,
                        ActivePowerW = energyData.ActivePowerW ?? 0,
                        TotalPowerImportT1KWh = energyData.TotalPowerImportT1KWh,
                        TotalPowerImportT2KWh = energyData.TotalPowerImportT2KWh,
                        TotalPowerExportT1KWh = energyData.TotalPowerExportT1KWh,
                        TotalPowerExportT2KWh = energyData.TotalPowerExportT2KWh,
                        TotalGasM3 = energyData.TotalGasM3
                    };

                    dbContext.EnergyReadings.Add(reading);

                    // Update last seen time - device is responding
                    device.LastSeenAt = DateTime.UtcNow;

                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation(
                        "Collected energy data from {DeviceName} ({ProductType}) at {IpAddress}: PowerUsage={PowerW}W",
                        device.Name, device.ProductType, device.IpAddress, energyData.ActivePowerW);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning("Device {DeviceName} has unsupported product type: {Message}",
                        device.Name, ex.Message);

                    // Disable unsupported devices to avoid repeated errors
                    device.IsEnabled = false;
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // This is expected when device is offline or not responding
                    // Don't update LastSeenAt - let the monitoring service handle alerts
                    _logger.LogDebug("Device {DeviceName} ({ProductType}) at {IpAddress} is not responding (timeout)",
                        device.Name, device.ProductType, device.IpAddress);
                }
                catch (HttpRequestException)
                {
                    // Network errors are expected when device is offline
                    // Don't update LastSeenAt - let the monitoring service handle alerts
                    _logger.LogDebug("Device {DeviceName} ({ProductType}) at {IpAddress} is not reachable",
                        device.Name, device.ProductType, device.IpAddress);
                }
                catch (Exception ex)
                {
                    // Only log actual errors, not expected communication failures
                    _logger.LogWarning(ex,
                        "Unexpected error collecting data from device {DeviceName} ({ProductType}) at {IpAddress}",
                        device.Name, device.ProductType, device.IpAddress);
                }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device polling");
        }
    }
}