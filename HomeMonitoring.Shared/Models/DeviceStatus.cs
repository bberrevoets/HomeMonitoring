namespace HomeMonitoring.Shared.Models;

public class DeviceStatus
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int DeviceId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastOfflineAlertSent { get; set; }
    public DateTime? WentOfflineAt { get; set; }
}