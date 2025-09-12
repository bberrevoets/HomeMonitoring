using System.ComponentModel.DataAnnotations;

namespace HomeMonitoring.Web.Models;

public class DeviceInputModel
{
    [Required(ErrorMessage = "IP Address is required")]
    [Display(Name = "IP Address")]
    [RegularExpression(@"^(?:[0-9]{1,3}\.){3}[0-9]{1,3}$", ErrorMessage = "Please enter a valid IP address")]
    public string IpAddress { get; init; } = string.Empty;

    [Display(Name = "Device Name (optional)")]
    public string? Name { get; init; }
}