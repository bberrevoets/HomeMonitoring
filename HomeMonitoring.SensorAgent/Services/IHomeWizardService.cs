using HomeMonitoring.SensorAgent.Models;
using HomeMonitoring.SensorAgent.Models.HomeWizard;

namespace HomeMonitoring.SensorAgent.Services;

public interface IHomeWizardService
{
    Task<DeviceResponse> GetDeviceInfoAsync(string ipAddress, CancellationToken cancellationToken = default);
    Task<EnergyResponse> GetEnergyDataAsync(string ipAddress, CancellationToken cancellationToken = default);
    Task<IEnergyDataResponse> GetEnergyDataAsync(string ipAddress, HomeWizardProductType productType, CancellationToken cancellationToken = default);
    Task DiscoverDevicesAsync(CancellationToken cancellationToken = default);
}