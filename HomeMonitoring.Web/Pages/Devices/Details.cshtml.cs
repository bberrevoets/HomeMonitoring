using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using HomeMonitoring.Shared.Models.HomeWizard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Web.Pages.Devices;

// Live device info and the reading count change constantly (SensorAgent writes every 10s), so the
// browser must never serve this page from its HTTP cache or back/forward (bfcache) snapshot —
// otherwise Refresh / navigating back shows a stale count. no-store also disables bfcache.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DetailsModel : PageModel
{
    private readonly SensorDbContext _context;
    private readonly IHomeWizardService _homeWizard;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(SensorDbContext context, IHomeWizardService homeWizard, ILogger<DetailsModel> logger)
    {
        _context = context;
        _homeWizard = homeWizard;
        _logger = logger;
    }

    public Device Device { get; private set; } = null!;

    // Live info fetched on demand from the device; null when the device is unreachable.
    public DeviceResponse? DeviceInfo { get; private set; }
    public EnergyResponse? EnergyData { get; private set; }
    public bool LiveDataAvailable => DeviceInfo is not null || EnergyData is not null;

    // Aggregate stats over the device's collected energy readings.
    public int ReadingCount { get; private set; }
    public DateTime? FirstReadingAt { get; private set; }
    public DateTime? LastReadingAt { get; private set; }
    public double? LatestActivePowerW { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device is null) return NotFound();

        Device = device;

        await LoadReadingStatsAsync(id);
        await LoadLiveDataAsync(device.IpAddress);

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

    private async Task LoadLiveDataAsync(string ipAddress)
    {
        var ct = HttpContext.RequestAborted;

        // Two independent calls: /api gives firmware/serial/product, /api/v1/data gives WiFi + live power.
        // Either can fail independently when the device is offline; show whatever we can reach. HomeWizard
        // devices accept very few simultaneous connections, so this on-demand fetch can briefly collide
        // with the SensorAgent's poll and get refused — retry a few times before giving up.
        DeviceInfo = await TryFetchAsync(() => _homeWizard.GetDeviceInfoAsync(ipAddress, ct), "device-info", ipAddress, ct);
        EnergyData = await TryFetchAsync(() => _homeWizard.GetEnergyDataAsync(ipAddress, ct), "energy-data", ipAddress, ct);
    }

    private async Task<T?> TryFetchAsync<T>(Func<Task<T>> fetch, string what, string ipAddress, CancellationToken ct)
        where T : class
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await fetch();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (attempt == attempts || ct.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Device {IpAddress} unreachable for {What} fetch (after {Attempts} attempts)",
                        ipAddress, what, attempt);
                    return null;
                }

                await Task.Delay(300, ct);
            }
        }

        return null;
    }
}
