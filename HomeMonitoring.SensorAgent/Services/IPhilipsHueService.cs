using HomeMonitoring.Shared.Models.PhilipsHue;

namespace HomeMonitoring.SensorAgent.Services;

public interface IPhilipsHueService
{
    Task<List<HueBridgeDiscoveryResponse>> DiscoverBridgesAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, HueLightResponse>> GetLightsAsync(string bridgeIp, string apiKey, CancellationToken cancellationToken = default);
    Task<HueLightResponse> GetLightAsync(string bridgeIp, string apiKey, string lightId, CancellationToken cancellationToken = default);
    Task UpdateLightStateAsync(string bridgeIp, string apiKey, string lightId, HueLightState state, CancellationToken cancellationToken = default);
    Task<string> RegisterApplicationAsync(string bridgeIp, string applicationName, string deviceName, CancellationToken cancellationToken = default);
}