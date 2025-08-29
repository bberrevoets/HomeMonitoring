namespace HomeMonitoring.SensorAgent.Services;

public interface IEmailService
{
    Task SendDeviceOfflineAlertAsync(string deviceName, string deviceType, string ipAddress, DateTime lastSeenAt,
        CancellationToken cancellationToken = default);

    Task SendDeviceBackOnlineAlertAsync(string deviceName, string deviceType, string ipAddress, DateTime offlineSince,
        CancellationToken cancellationToken = default);
}