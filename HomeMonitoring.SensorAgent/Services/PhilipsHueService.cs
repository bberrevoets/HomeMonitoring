using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models.PhilipsHue;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.SensorAgent.Services;

public class PhilipsHueService : IPhilipsHueService
{
    private readonly SensorDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PhilipsHueService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PhilipsHueService(
        SensorDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<PhilipsHueService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<HueBridgeDiscoveryResponse>> DiscoverBridgesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync("https://discovery.meethue.com/", cancellationToken);
            response.EnsureSuccessStatusCode();

            var bridges = await response.Content.ReadFromJsonAsync<List<HueBridgeDiscoveryResponse>>(_jsonOptions, cancellationToken);
            return bridges ?? new List<HueBridgeDiscoveryResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering Hue bridges");
            return new List<HueBridgeDiscoveryResponse>();
        }
    }

    public async Task<Dictionary<string, HueLightResponse>> GetLightsAsync(string bridgeIp, string apiKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync($"http://{bridgeIp}/api/{apiKey}/lights", cancellationToken);
            response.EnsureSuccessStatusCode();

            var lights = await response.Content.ReadFromJsonAsync<Dictionary<string, HueLightResponse>>(_jsonOptions, cancellationToken);
            return lights ?? new Dictionary<string, HueLightResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lights from bridge {BridgeIp}", bridgeIp);
            return new Dictionary<string, HueLightResponse>();
        }
    }

    public async Task<HueLightResponse> GetLightAsync(string bridgeIp, string apiKey, string lightId, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync($"http://{bridgeIp}/api/{apiKey}/lights/{lightId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var light = await response.Content.ReadFromJsonAsync<HueLightResponse>(_jsonOptions, cancellationToken);
            return light ?? new HueLightResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting light {LightId} from bridge {BridgeIp}", lightId, bridgeIp);
            throw;
        }
    }

    public async Task UpdateLightStateAsync(string bridgeIp, string apiKey, string lightId, HueLightState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PutAsync($"http://{bridgeIp}/api/{apiKey}/lights/{lightId}/state", content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating light {LightId} on bridge {BridgeIp}", lightId, bridgeIp);
            throw;
        }
    }

    public async Task<string> RegisterApplicationAsync(string bridgeIp, string applicationName, string deviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var requestBody = new
            {
                devicetype = $"{applicationName}#{deviceName}"
            };

            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"http://{bridgeIp}/api", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseArray = JsonSerializer.Deserialize<JsonElement[]>(responseText, _jsonOptions);

            if (responseArray != null && responseArray.Length > 0)
            {
                var firstResponse = responseArray[0];
                
                if (firstResponse.TryGetProperty("success", out var success))
                {
                    if (success.TryGetProperty("username", out var username))
                    {
                        return username.GetString() ?? string.Empty;
                    }
                }
                else if (firstResponse.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("description", out var description))
                    {
                        throw new InvalidOperationException($"Bridge error: {description.GetString()}");
                    }
                }
            }

            throw new InvalidOperationException("Unexpected response from bridge");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering application with bridge {BridgeIp}", bridgeIp);
            throw;
        }
    }

    public async Task SyncLightsAsync(string bridgeIp, CancellationToken cancellationToken = default)
    {
        try
        {
            var bridgeConfig = await _dbContext.HueBridgeConfigurations
                .FirstOrDefaultAsync(b => b.IpAddress == bridgeIp && b.IsEnabled, cancellationToken);

            if (bridgeConfig == null)
            {
                _logger.LogWarning("No enabled bridge configuration found for {BridgeIp}", bridgeIp);
                return;
            }

            var lights = await GetLightsAsync(bridgeIp, bridgeConfig.ApiKey, cancellationToken);

            foreach (var (lightId, lightData) in lights)
            {
                var existingLight = await _dbContext.HueLights
                    .FirstOrDefaultAsync(l => l.HueId == lightId && l.BridgeIpAddress == bridgeIp, cancellationToken);

                if (existingLight == null)
                {
                    // New light discovered
                    existingLight = new HueLight
                    {
                        HueId = lightId,
                        Name = lightData.Name,
                        ModelId = lightData.ModelId,
                        ManufacturerName = lightData.ManufacturerName,
                        ProductName = lightData.ProductName,
                        BridgeIpAddress = bridgeIp,
                        DiscoveredAt = DateTime.UtcNow,
                        LastSeenAt = DateTime.UtcNow,
                        IsEnabled = true
                    };

                    _dbContext.HueLights.Add(existingLight);
                    _logger.LogInformation("Discovered new Hue light: {LightName} (ID: {LightId})", lightData.Name, lightId);
                }
                else
                {
                    // Update existing light
                    existingLight.Name = lightData.Name;
                    existingLight.LastSeenAt = DateTime.UtcNow;
                }

                // Add a reading for the current state
                var reading = new HueLightReading
                {
                    HueLightId = existingLight.Id,
                    Timestamp = DateTime.UtcNow,
                    On = lightData.State.On,
                    Brightness = lightData.State.Brightness ?? 0,
                    Hue = lightData.State.Hue,
                    Saturation = lightData.State.Saturation,
                    ColorTemperature = lightData.State.ColorTemperature,
                    Reachable = lightData.State.Reachable
                };

                _dbContext.HueLightReadings.Add(reading);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing lights from bridge {BridgeIp}", bridgeIp);
        }
    }
}
