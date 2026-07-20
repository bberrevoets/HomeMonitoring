using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Web.Pages.Devices;

// The reading count and last-known status change constantly (the SensorAgent writes every 10s), so the
// browser must never serve this page from its HTTP cache or back/forward (bfcache) snapshot.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DetailsModel : PageModel
{
    private readonly SensorDbContext _context;

    public DetailsModel(SensorDbContext context) => _context = context;

    // All values come from the DB — the SensorAgent is the only client that contacts the (single-
    // connection) device, so the web app reads the status it persisted rather than polling directly.
    public Device Device { get; private set; } = null!;

    public int ReadingCount { get; private set; }
    public DateTime? FirstReadingAt { get; private set; }
    public DateTime? LastReadingAt { get; private set; }
    public double? LatestActivePowerW { get; private set; }

    public bool HasStatus =>
        Device.FirmwareVersion is not null || Device.WifiSsid is not null || LatestActivePowerW is not null;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device is null) return NotFound();

        Device = device;
        await LoadReadingStatsAsync(id);

        return Page();
    }

    private async Task LoadReadingStatsAsync(int id)
    {
        var readings = _context.EnergyReadings.Where(e => e.DeviceId == id);

        ReadingCount = await readings.CountAsync();
        if (ReadingCount == 0) return;

        FirstReadingAt = await readings.MinAsync(e => e.Timestamp);
        LastReadingAt = await readings.MaxAsync(e => e.Timestamp);
        LatestActivePowerW = await readings
            .OrderByDescending(e => e.Timestamp)
            .Select(e => e.ActivePowerW)
            .FirstOrDefaultAsync();
    }
}
