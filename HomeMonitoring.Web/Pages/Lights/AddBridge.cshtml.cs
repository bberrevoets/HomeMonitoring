using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models.PhilipsHue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Web.Pages.Lights;

public class AddBridgeModel : PageModel
{
    private readonly SensorDbContext _context;
    private readonly IPhilipsHueService _hueService;
    private readonly ILogger<AddBridgeModel> _logger;

    public AddBridgeModel(SensorDbContext context, IPhilipsHueService hueService, ILogger<AddBridgeModel> logger)
    {
        _context = context;
        _hueService = hueService;
        _logger = logger;
    }

    [BindProperty]
    public string? BridgeIp { get; set; }

    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }
    public bool WaitingForButtonPress { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostDiscoverAsync()
    {
        // Clear model state to avoid validation issues
        ModelState.Clear();
        
        try
        {
            var bridges = await _hueService.DiscoverBridgesAsync();
            if (bridges.Any())
            {
                BridgeIp = bridges.First().InternalIpAddress;
                InfoMessage = $"Found {bridges.Count} bridge(s). Using {BridgeIp}";
            }
            else
            {
                InfoMessage = "No bridges found on the network. Please enter the IP address manually.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering Hue bridges");
            ErrorMessage = "Failed to discover bridges. Please enter the IP address manually.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(BridgeIp))
        {
            ErrorMessage = "Please enter a bridge IP address or use Auto Discover first.";
            return Page();
        }

        try
        {
            // Check if bridge already exists
            var existingBridge = await _context.HueBridgeConfigurations
                .FirstOrDefaultAsync(b => b.IpAddress == BridgeIp);

            if (existingBridge != null)
            {
                ErrorMessage = "This bridge is already configured.";
                return Page();
            }

            // Try to register with the bridge
            var apiKey = await _hueService.RegisterApplicationAsync(BridgeIp, "HomeMonitoring", Environment.MachineName);

            // Get bridge info to get the bridge ID
            var lights = await _hueService.GetLightsAsync(BridgeIp, apiKey);
            
            var bridge = new HueBridgeConfiguration
            {
                BridgeId = Guid.NewGuid().ToString(), // In a real app, you'd get this from the bridge config endpoint
                IpAddress = BridgeIp,
                ApiKey = apiKey,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true
            };

            _context.HueBridgeConfigurations.Add(bridge);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("link button not pressed"))
        {
            WaitingForButtonPress = true;
            InfoMessage = "Please press the link button on your Hue bridge and try again within 30 seconds.";
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering with Hue bridge at {BridgeIp}", BridgeIp);
            ErrorMessage = $"Failed to register with bridge: {ex.Message}";
            return Page();
        }
    }
}