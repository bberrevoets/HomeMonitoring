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
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(SensorDbContext context, ILogger<IndexModel> logger)
    {
        _context = context;
        _logger = logger;
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

        // 1. Disable first: the SensorAgent rereads the enabled devices every cycle, so polling stops within one
        //    cycle even if the rest takes minutes or is interrupted. A second click continues where this one stopped.
        if (device.IsEnabled)
        {
            device.IsEnabled = false;
            await _context.SaveChangesAsync();
        }

        // 2. Remove the readings in batches. Leaving them to the cascade delete puts months of readings into one
        //    statement, which ran past SQL Server's 30 s command timeout and failed (2026-11-10). Each batch uses
        //    the (DeviceId, Timestamp) index. No request token on purpose: a closed browser tab must not stop it.
        var removed = 0;
        int batch;
        do
        {
            batch = await _context.EnergyReadings
                .Where(r => r.DeviceId == id)
                .Take(DeleteBatchSize)
                .ExecuteDeleteAsync();
            removed += batch;
        } while (batch > 0);

        // 3. The device itself, now without readings.
        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted device {DeviceName} ({DeviceId}) and {ReadingCount} readings",
            device.Name, id, removed);
        TempData["SuccessMessage"] = $"Deleted device '{device.Name}' and {removed:N0} readings.";
        return RedirectToPage("./Index");
    }

    private const int DeleteBatchSize = 5000;
}