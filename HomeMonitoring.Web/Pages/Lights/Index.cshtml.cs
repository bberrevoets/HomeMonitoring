using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models.PhilipsHue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Web.Pages.Lights;

public class IndexModel : PageModel
{
    private readonly SensorDbContext _context;
    private readonly IPhilipsHueService _hueService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(SensorDbContext context, IPhilipsHueService hueService, ILogger<IndexModel> logger)
    {
        _context = context;
        _hueService = hueService;
        _logger = logger;
    }

    public List<BridgeWithLights> Bridges { get; set; } = new();

    public async Task OnGetAsync()
    {
        var bridges = await _context.HueBridgeConfigurations
            .Where(b => b.IsEnabled)
            .ToListAsync();

        foreach (var bridge in bridges)
        {
            var bridgeModel = new BridgeWithLights
            {
                BridgeId = bridge.BridgeId,
                IpAddress = bridge.IpAddress,
                IsEnabled = bridge.IsEnabled,
                Lights = new List<LightInfo>()
            };

            try
            {
                var lights = await _hueService.GetLightsAsync(bridge.IpAddress, bridge.ApiKey);
                foreach (var kvp in lights)
                {
                    bridgeModel.Lights.Add(new LightInfo
                    {
                        Id = kvp.Key,
                        Name = kvp.Value.Name,
                        Type = kvp.Value.Type,
                        IsOn = kvp.Value.State.On,
                        Brightness = kvp.Value.State.Brightness,
                        IsReachable = kvp.Value.State.Reachable
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get lights from bridge {BridgeId}", bridge.BridgeId);
            }

            Bridges.Add(bridgeModel);
        }
    }

    public async Task<IActionResult> OnPostToggleLightAsync(string bridgeId, string lightId, bool on, byte? brightness = null)
    {
        try
        {
            var bridge = await _context.HueBridgeConfigurations
                .FirstOrDefaultAsync(b => b.BridgeId == bridgeId);

            if (bridge == null)
            {
                return BadRequest("Bridge not found");
            }

            // Create a new state with the on/off property
            var state = new HueLightState { On = on };
            
            // If turning on and brightness is provided, include it
            if (on && brightness.HasValue)
            {
                state.Brightness = brightness.Value;
            }
            
            await _hueService.UpdateLightStateAsync(bridge.IpAddress, bridge.ApiKey, lightId, state);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle light {LightId} on bridge {BridgeId}", lightId, bridgeId);
            return BadRequest("Failed to toggle light");
        }
    }

    public async Task<IActionResult> OnPostSetBrightnessAsync(string bridgeId, string lightId, byte brightness)
    {
        try
        {
            var bridge = await _context.HueBridgeConfigurations
                .FirstOrDefaultAsync(b => b.BridgeId == bridgeId);

            if (bridge == null)
            {
                return BadRequest("Bridge not found");
            }

            // When setting brightness on an already-on light, ensure it stays on
            var state = new HueLightState 
            { 
                Brightness = brightness,
                On = true
            };
            await _hueService.UpdateLightStateAsync(bridge.IpAddress, bridge.ApiKey, lightId, state);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set brightness for light {LightId} on bridge {BridgeId}", lightId, bridgeId);
            return BadRequest("Failed to set brightness");
        }
    }

    public async Task<IActionResult> OnPostRefreshLightsAsync(string bridgeId)
    {
        try
        {
            var bridge = await _context.HueBridgeConfigurations
                .FirstOrDefaultAsync(b => b.BridgeId == bridgeId);

            if (bridge == null)
            {
                return BadRequest("Bridge not found");
            }

            // Just validate we can connect
            await _hueService.GetLightsAsync(bridge.IpAddress, bridge.ApiKey);
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh lights for bridge {BridgeId}", bridgeId);
            return BadRequest("Failed to refresh lights");
        }
    }

    public class BridgeWithLights
    {
        public string BridgeId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public List<LightInfo> Lights { get; set; } = new();
    }

    public class LightInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsOn { get; set; }
        public byte? Brightness { get; set; }
        public bool IsReachable { get; set; }
    }
}