using Microsoft.AspNetCore.Mvc.RazorPages;
using HomeMonitoring.Web.Services;
using HomeMonitoring.Web.Models;

namespace HomeMonitoring.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDashboardService _dashboardService;

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