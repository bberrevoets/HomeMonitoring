using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models.PhilipsHue;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.SensorAgent.Services;

public class HueLightMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HueLightMonitoringService> _logger;
    private readonly int _pollingIntervalSeconds;

    public HueLightMonitoringService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<HueLightMonitoringService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pollingIntervalSeconds = configuration.GetValue("HueMonitoring:PollingIntervalSeconds", 10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hue Light Monitoring Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollHueLightsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Hue Light monitoring loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Hue Light Monitoring Service stopped");
    }

    private async Task PollHueLightsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
        var hueService = scope.ServiceProvider.GetRequiredService<IPhilipsHueService>();

        // Get all enabled bridge configurations
        var bridges = await dbContext.HueBridgeConfigurations
            .Where(b => b.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var bridge in bridges)
        {
            try
            {
                await ProcessBridgeAsync(bridge, dbContext, hueService, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Hue bridge {BridgeId} at {IpAddress}", 
                    bridge.BridgeId, bridge.IpAddress);
            }
        }
    }

    private async Task ProcessBridgeAsync(
        HueBridgeConfiguration bridge,
        SensorDbContext dbContext,
        IPhilipsHueService hueService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all lights from the bridge
            var lights = await hueService.GetLightsAsync(bridge.IpAddress, bridge.ApiKey, cancellationToken);

            foreach (var (lightId, lightData) in lights)
            {
                try
                {
                    // Find or create the light in database
                    var hueLight = await dbContext.HueLights
                        .FirstOrDefaultAsync(l => l.HueId == lightId && l.BridgeIpAddress == bridge.IpAddress, cancellationToken);

                    if (hueLight == null)
                    {
                        hueLight = new HueLight
                        {
                            HueId = lightId,
                            Name = lightData.Name,
                            ModelId = lightData.ModelId,
                            ManufacturerName = lightData.ManufacturerName,
                            ProductName = lightData.ProductName,
                            BridgeIpAddress = bridge.IpAddress,
                            DiscoveredAt = DateTime.UtcNow,
                            LastSeenAt = DateTime.UtcNow,
                            IsEnabled = true
                        };
                        dbContext.HueLights.Add(hueLight);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        
                        _logger.LogInformation("Discovered new Hue light: {LightName} (ID: {LightId}) on bridge {BridgeIp}", 
                            lightData.Name, lightId, bridge.IpAddress);
                    }
                    else
                    {
                        // Update existing light
                        hueLight.Name = lightData.Name;
                        hueLight.LastSeenAt = DateTime.UtcNow;
                    }

                    // Only record readings for enabled lights
                    if (hueLight.IsEnabled)
                    {
                        var reading = new HueLightReading
                        {
                            HueLightId = hueLight.Id,
                            Timestamp = DateTime.UtcNow,
                            On = lightData.State.On,
                            Brightness = lightData.State.Brightness ?? 0,
                            Hue = lightData.State.Hue,
                            Saturation = lightData.State.Saturation,
                            ColorTemperature = lightData.State.ColorTemperature,
                            Reachable = lightData.State.Reachable
                        };

                        dbContext.HueLightReadings.Add(reading);
                        _logger.LogDebug("Recorded reading for light {LightName}: On={On}, Brightness={Brightness}, Reachable={Reachable}",
                            hueLight.Name, reading.On, reading.Brightness, reading.Reachable);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing light {LightId} on bridge {BridgeIp}", 
                        lightId, bridge.IpAddress);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("Bridge {BridgeId} at {IpAddress} is not reachable: {Message}", 
                bridge.BridgeId, bridge.IpAddress, ex.Message);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Bridge {BridgeId} at {IpAddress} did not respond within timeout", 
                bridge.BridgeId, bridge.IpAddress);
        }
    }
}