using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Models;
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

    public IList<Device> Devices { get; set; } = default!;

    public async Task OnGetAsync()
    {
        Devices = await _context.Devices.ToListAsync();
    }
}