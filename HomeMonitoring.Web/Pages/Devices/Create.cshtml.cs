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
                // Parse the product type
                var productType = ParseProductType(response.ProductType);

                // Check if we support this product type
                if (productType != HomeWizardProductType.HWE_P1 && productType != HomeWizardProductType.HWE_SKT)
                {
                    ModelState.AddModelError(string.Empty,
                        $"The device type '{response.ProductType}' is not currently supported. Only HWE-P1 and HWE-SKT devices are supported.");
                    return Page();
                }

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
                    ProductType = productType,
                    ProductTypeRaw = response.ProductType,
                    SerialNumber = response.SerialNumber,
                    DiscoveredAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow,
                    IsEnabled = true
                };

                _context.Devices.Add(device);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully added {response.ProductType} device: {device.Name}";
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

    private static HomeWizardProductType ParseProductType(string productTypeString)
    {
        return productTypeString switch
        {
            "HWE-P1" => HomeWizardProductType.HWE_P1,
            "HWE-SKT" => HomeWizardProductType.HWE_SKT,
            "HWE-WTR" => HomeWizardProductType.HWE_WTR,
            "HWE-KWH1" => HomeWizardProductType.HWE_KWH1,
            "HWE-KWH3" => HomeWizardProductType.HWE_KWH3,
            "SDM230-wifi" => HomeWizardProductType.SDM230_wifi,
            "SDM630-wifi" => HomeWizardProductType.SDM630_wifi,
            "HWE-DSP" => HomeWizardProductType.HWE_DSP,
            "HWE-BAT" => HomeWizardProductType.HWE_BAT,
            _ => HomeWizardProductType.Unknown
        };
    }
}