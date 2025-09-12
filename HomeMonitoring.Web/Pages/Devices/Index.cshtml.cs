using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.Web.Pages.Devices;

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
}