using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using HomeMonitoring.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeMonitoring.Web.Pages.Devices;

public class EditModel : PageModel
{
    private readonly SensorDbContext _context;

    public EditModel(SensorDbContext context) => _context = context;

    [BindProperty] public EditDeviceInputModel Input { get; set; } = new();

    // Read-only context so the user knows which device they're renaming.
    public string SerialNumber { get; private set; } = string.Empty;
    public string IpAddress { get; private set; } = string.Empty;
    public HomeWizardProductType ProductType { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device is null) return NotFound();

        Input.Name = device.Name;
        PopulateDisplay(device);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device is null) return NotFound();

        if (!ModelState.IsValid)
        {
            PopulateDisplay(device);
            return Page();
        }

        device.Name = Input.Name.Trim();
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Renamed device to '{device.Name}'.";
        return RedirectToPage("./Index");
    }

    private void PopulateDisplay(Device device)
    {
        SerialNumber = device.SerialNumber;
        IpAddress = device.IpAddress;
        ProductType = device.ProductType;
    }
}
