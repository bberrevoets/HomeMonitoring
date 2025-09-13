using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models.PhilipsHue;
using HomeMonitoring.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace HomeMonitoring.Web.Services
{
    public class PhilipsHueLightMonitorService : BackgroundService
    {
        private readonly ILogger<PhilipsHueLightMonitorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<LightsHub> _hubContext;
        private readonly ConcurrentDictionary<string, Dictionary<string, LightState>> _previousStates = new();

        public PhilipsHueLightMonitorService(
            ILogger<PhilipsHueLightMonitorService> logger,
            IServiceProvider serviceProvider,
            IHubContext<LightsHub> hubContext)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForLightChanges(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); // Poll every 2 seconds
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Philips Hue light monitor service");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Wait longer on error
                }
            }
        }

        private async Task CheckForLightChanges(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
            var hueService = scope.ServiceProvider.GetRequiredService<IPhilipsHueService>();

            var bridges = await context.HueBridgeConfigurations
                .Where(b => b.IsEnabled)
                .ToListAsync(cancellationToken);

            foreach (var bridge in bridges)
            {
                try
                {
                    var currentLights = await hueService.GetLightsAsync(bridge.IpAddress, bridge.ApiKey);
                    
                    // Get previous state for this bridge
                    if (!_previousStates.TryGetValue(bridge.BridgeId, out var previousLights))
                    {
                        // First time seeing this bridge, just store the state
                        _previousStates[bridge.BridgeId] = ConvertToLightStates(currentLights);
                        continue;
                    }

                    // Check for changes
                    var changes = new List<LightChangeInfo>();
                    
                    foreach (var kvp in currentLights)
                    {
                        var lightId = kvp.Key;
                        var currentLight = kvp.Value;
                        
                        if (previousLights.TryGetValue(lightId, out var previousState))
                        {
                            if (HasLightChanged(previousState, currentLight))
                            {
                                changes.Add(new LightChangeInfo
                                {
                                    BridgeId = bridge.BridgeId,
                                    LightId = lightId,
                                    Name = currentLight.Name,
                                    IsOn = currentLight.State.On,
                                    Brightness = currentLight.State.Brightness,
                                    IsReachable = currentLight.State.Reachable
                                });
                            }
                        }
                        else
                        {
                            // New light added
                            changes.Add(new LightChangeInfo
                            {
                                BridgeId = bridge.BridgeId,
                                LightId = lightId,
                                Name = currentLight.Name,
                                IsOn = currentLight.State.On,
                                Brightness = currentLight.State.Brightness,
                                IsReachable = currentLight.State.Reachable,
                                IsNew = true
                            });
                        }
                    }

                    // Update stored state
                    _previousStates[bridge.BridgeId] = ConvertToLightStates(currentLights);

                    // Broadcast changes if any
                    if (changes.Any())
                    {
                        _logger.LogInformation("Detected {Count} light changes on bridge {BridgeId}", 
                            changes.Count, bridge.BridgeId);
                        
                        await _hubContext.Clients.Group("LightsPage")
                            .SendAsync("LightStateChanged", changes, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking lights for bridge {BridgeId}", bridge.BridgeId);
                }
            }
        }

        private Dictionary<string, LightState> ConvertToLightStates(Dictionary<string, HueLightResponse> lights)
        {
            return lights.ToDictionary(
                kvp => kvp.Key,
                kvp => new LightState
                {
                    IsOn = kvp.Value.State.On,
                    Brightness = kvp.Value.State.Brightness,
                    IsReachable = kvp.Value.State.Reachable
                });
        }

        private bool HasLightChanged(LightState previous, HueLightResponse current)
        {
            return previous.IsOn != current.State.On ||
                   previous.Brightness != current.State.Brightness ||
                   previous.IsReachable != current.State.Reachable;
        }

        private class LightState
        {
            public bool IsOn { get; set; }
            public byte? Brightness { get; set; }
            public bool IsReachable { get; set; }
        }

        public class LightChangeInfo
        {
            public string BridgeId { get; set; } = string.Empty;
            public string LightId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool IsOn { get; set; }
            public byte? Brightness { get; set; }
            public bool IsReachable { get; set; }
            public bool IsNew { get; set; }
        }
    }
}