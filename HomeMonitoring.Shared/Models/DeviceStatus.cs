namespace HomeMonitoring.Shared.Models;

public class DeviceStatus
{
    public int DeviceId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastOfflineAlertSent { get; set; }
    public DateTime? WentOfflineAt { get; set; }
}