using System.ComponentModel.DataAnnotations;

namespace HomeMonitoring.Web.Models;

public class EditDeviceInputModel
{
    [Required(ErrorMessage = "Device name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Device name must be 1-200 characters")]
    [Display(Name = "Device Name")]
    public string Name { get; set; } = string.Empty;
}
