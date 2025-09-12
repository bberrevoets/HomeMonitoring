using System.Diagnostics.Metrics;
using HomeMonitoring.Shared.Data;

namespace HomeMonitoring.SensorAgent.Metrics;

public class SensorAgentMetrics : IDisposable
{
    private readonly Counter<int> _deviceErrors;
    private readonly Meter _meter;
    private readonly Histogram<double> _sensorReadingProcessingTime;
    private readonly Counter<int> _sensorReadingsProcessed;
    private readonly IServiceProvider _serviceProvider;

    public SensorAgentMetrics(IMeterFactory meterFactory, IServiceProvider serviceProvider)
    {
        _meter = meterFactory.Create("HomeMonitoring.SensorAgent");
        _serviceProvider = serviceProvider;

        _sensorReadingsProcessed = _meter.CreateCounter<int>(
            "sensor_readings_processed_total",
            "The total number of sensor readings processed");

        _deviceErrors = _meter.CreateCounter<int>(
            "device_errors_total",
            "The total number of device communication errors");

        _sensorReadingProcessingTime = _meter.CreateHistogram<double>(
            "sensor_reading_processing_duration_seconds",
            "The time taken to process a sensor reading");

        _meter.CreateObservableGauge(
            "active_devices_count",
            GetActiveDevicesCount,
            "The number of active devices");

        _meter.CreateObservableGauge(
            "offline_devices_count",
            GetOfflineDevicesCount,
            "The number of offline devices");
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }

    private int GetActiveDevicesCount()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
            var offlineThreshold = TimeSpan.FromMinutes(5);
            return context.Devices.Count(d => d.LastSeenAt > DateTime.UtcNow - offlineThreshold);
        }
        catch
        {
            return 0;
        }
    }

    private int GetOfflineDevicesCount()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
            var offlineThreshold = TimeSpan.FromMinutes(5);
            return context.Devices.Count(d => d.LastSeenAt <= DateTime.UtcNow - offlineThreshold);
        }
        catch
        {
            return 0;
        }
    }

    public void IncrementSensorReadingsProcessed()
    {
        _sensorReadingsProcessed.Add(1);
    }

    public void IncrementDeviceErrors()
    {
        _deviceErrors.Add(1);
    }

    public void RecordProcessingTime(double seconds)
    {
        _sensorReadingProcessingTime.Record(seconds);
    }
}