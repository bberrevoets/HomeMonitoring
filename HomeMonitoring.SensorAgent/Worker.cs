using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Models;
using HomeMonitoring.SensorAgent.Services;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.SensorAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

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
        
        while (await pollingTimer.WaitForNextTickAsync(stoppingToken))
        {
            await PollDevicesAsync(stoppingToken);
        }
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
            {
                try
                {
                    var energyData = await homeWizardService.GetEnergyDataAsync(device.IpAddress, stoppingToken);
                    
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
                    };

                    dbContext.EnergyReadings.Add(reading);
                    
                    // Update last seen time
                    device.LastSeenAt = DateTime.UtcNow;
                    
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Collected energy data from {DeviceName} at {IpAddress}: PowerUsage={PowerW}W", 
                        device.Name, device.IpAddress, energyData.ActivePowerW);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting data from device {DeviceName} at {IpAddress}", 
                        device.Name, device.IpAddress);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device polling");
        }
    }
}
