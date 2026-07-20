using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Web.Pages.Devices;

// The list's Last Seen / Status columns change constantly, so the browser must never serve this page
// from its HTTP cache or back/forward (bfcache) snapshot. no-store also disables bfcache.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel : PageModel
{
    private readonly SensorDbContext _context;

    public IndexModel(SensorDbContext context)
    {
        _context = context;
    }

    public IList<Device> Devices { get; set; } = null!;

    public async Task OnGetAsync()
    {
        Devices = await _context.Devices.ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device is null) return NotFound();

        // EnergyReadings are removed automatically via the configured cascade delete.
        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Deleted device '{device.Name}' and all its collected data.";
        return RedirectToPage("./Index");
    }
}