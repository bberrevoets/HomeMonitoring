using HomeMonitoring.Web.Models;
using HomeMonitoring.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomeMonitoring.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger, IDashboardService dashboardService)
    {
        _logger = logger;
        _dashboardService = dashboardService;
    }

    public DashboardData DashboardData { get; set; } = new();

    public async Task OnGetAsync()
    {
        DashboardData = await _dashboardService.GetDashboardDataAsync();
    }
}