using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Models;
using HomeMonitoring.SensorAgent.Models.HomeWizard;
using HomeMonitoring.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Web.Pages.Devices;

public class CreateModel : PageModel
{
    private readonly SensorDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(SensorDbContext context, IHttpClientFactory httpClientFactory, ILogger<CreateModel> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty] public DeviceInputModel Input { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            // Try to connect to the device and get its information
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5); // Add timeout

            var response = await client.GetFromJsonAsync<DeviceResponse>(
                $"http://{Input.IpAddress}/api");

            if (response != null)
            {
                // Check if device already exists
                var existingDevice = await _context.Devices
                    .FirstOrDefaultAsync(d => d.SerialNumber == response.SerialNumber);

                if (existingDevice != null)
                {
                    ModelState.AddModelError(string.Empty, "This device is already registered.");
                    return Page();
                }

                var device = new Device
                {
                    Name = string.IsNullOrWhiteSpace(Input.Name) ? response.ProductName : Input.Name,
                    IpAddress = Input.IpAddress,
                    ProductType = response.ProductType,
                    SerialNumber = response.SerialNumber,
                    DiscoveredAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow,
                    IsEnabled = true
                };

                _context.Devices.Add(device);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully added device: {device.Name}";
                return RedirectToPage("./Index");
            }

            ModelState.AddModelError(string.Empty, "Could not connect to the device. Please check the IP address.");
            return Page();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error connecting to device at {IpAddress}", Input.IpAddress);
            ModelState.AddModelError(string.Empty,
                "Could not connect to the device. Please check that the IP address is correct and the device is online.");
            return Page();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout connecting to device at {IpAddress}", Input.IpAddress);
            ModelState.AddModelError(string.Empty,
                "Connection timed out. Please check that the IP address is correct and the device is online.");
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding device at {IpAddress}", Input.IpAddress);
            ModelState.AddModelError(string.Empty,
                "An error occurred while adding the device. Please ensure it's a HomeWizard device.");
            return Page();
        }
    }
}