using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Models;
using HomeMonitoring.SensorAgent.Models.HomeWizard;
using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.SensorAgent.Services;

public class HomeWizardService : IHomeWizardService
{
    private readonly SensorDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<HomeWizardService> _logger;

    public HomeWizardService(
        IHttpClientFactory httpClientFactory,
        SensorDbContext dbContext,
        ILogger<HomeWizardService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _dbContext = dbContext;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
    }

    public async Task<DeviceResponse> GetDeviceInfoAsync(string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"http://{ipAddress}/api", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DeviceResponse>(json, _jsonOptions);

            if (result == null) throw new Exception($"Failed to deserialize device info from {ipAddress}");

            return result;
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("Device at {IpAddress} did not respond within timeout (device info)", ipAddress);
            throw;
        }
        catch (HttpRequestException)
        {
            _logger.LogDebug("Device at {IpAddress} is not reachable (device info)", ipAddress);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info from {IpAddress}", ipAddress);
            throw;
        }
    }

    public async Task<EnergyResponse> GetEnergyDataAsync(string ipAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"http://{ipAddress}/api/v1/data", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Received JSON from {IpAddress}: {Json}", ipAddress, json);

            var result = JsonSerializer.Deserialize<EnergyResponse>(json, _jsonOptions);

            if (result == null) throw new Exception($"Failed to deserialize energy data from {ipAddress}");

            return result;
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("Device at {IpAddress} did not respond within timeout (energy data)", ipAddress);
            throw;
        }
        catch (HttpRequestException)
        {
            _logger.LogDebug("Device at {IpAddress} is not reachable (energy data)", ipAddress);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for energy data from {IpAddress}", ipAddress);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting energy data from {IpAddress}", ipAddress);
            throw;
        }
    }

    public async Task<IEnergyDataResponse> GetEnergyDataAsync(string ipAddress, HomeWizardProductType productType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"http://{ipAddress}/api/v1/data", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Received JSON from {IpAddress} for product {ProductType}: {Json}", ipAddress, productType,
                json);

            IEnergyDataResponse? result = productType switch
            {
                HomeWizardProductType.HWE_P1 => JsonSerializer.Deserialize<P1MeterResponse>(json, _jsonOptions),
                HomeWizardProductType.HWE_SKT => JsonSerializer.Deserialize<SocketResponse>(json, _jsonOptions),
                _ => throw new NotSupportedException($"Product type {productType} is not yet supported")
            };

            if (result == null) throw new Exception($"Failed to deserialize energy data from {ipAddress}");

            return result;
        }
        catch (TaskCanceledException)
        {
            // This is expected when device is offline - don't log as error
            _logger.LogDebug("Device at {IpAddress} did not respond within timeout for product type {ProductType}",
                ipAddress, productType);
            throw;
        }
        catch (HttpRequestException)
        {
            // Network errors are expected when device is offline - don't log as error
            _logger.LogDebug("Device at {IpAddress} is not reachable for product type {ProductType}",
                ipAddress, productType);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JSON deserialization error for energy data from {IpAddress} with product type {ProductType}",
                ipAddress, productType);
            throw;
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            _logger.LogError(ex,
                "Unexpected error getting energy data from {IpAddress} with product type {ProductType}",
                ipAddress, productType);
            throw;
        }
    }

    public async Task DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting HomeWizard device discovery");

        // Get local IP to determine network range
        var localIp = await GetLocalIpAddressAsync();
        if (localIp == null)
        {
            _logger.LogWarning("Could not determine local IP address for discovery");
            return;
        }

        var ipParts = localIp.Split('.');
        var networkPrefix = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";

        var discoveryTasks = new List<Task>();

        for (var i = 1; i <= 254; i++)
        {
            var ip = $"{networkPrefix}.{i}";
            var task = PingAndCheckDeviceAsync(ip, cancellationToken);
            discoveryTasks.Add(task);
        }

        await Task.WhenAll(discoveryTasks);
        _logger.LogInformation("HomeWizard device discovery completed");
    }

    private static HomeWizardProductType ParseProductType(string productTypeString)
    {
        return productTypeString switch
        {
            "HWE-P1" => HomeWizardProductType.HWE_P1,
            "HWE-SKT" => HomeWizardProductType.HWE_SKT,
            "HWE-WTR" => HomeWizardProductType.HWE_WTR,
            "HWE-KWH1" => HomeWizardProductType.HWE_KWH1,
            "HWE-KWH3" => HomeWizardProductType.HWE_KWH3,
            "SDM230-wifi" => HomeWizardProductType.SDM230_wifi,
            "SDM630-wifi" => HomeWizardProductType.SDM630_wifi,
            "HWE-DSP" => HomeWizardProductType.HWE_DSP,
            "HWE-BAT" => HomeWizardProductType.HWE_BAT,
            _ => HomeWizardProductType.Unknown
        };
    }

    private async Task<string?> GetLocalIpAddressAsync()
    {
        var host = await Dns.GetHostEntryAsync(Dns.GetHostName());
        foreach (var ip in host.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();

        return null;
    }

    private async Task PingAndCheckDeviceAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            // First ping to check if device is online
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 100);

            if (reply.Status != IPStatus.Success) return;

            // Try to get device info to check if it's a HomeWizard device
            try
            {
                var deviceInfo = await GetDeviceInfoAsync(ipAddress, cancellationToken);

                // Parse the product type
                var productType = ParseProductType(deviceInfo.ProductType);

                // Check if we support this product type
                if (productType != HomeWizardProductType.HWE_P1 && productType != HomeWizardProductType.HWE_SKT)
                {
                    _logger.LogInformation("Discovered unsupported HomeWizard device type {ProductType} at {IpAddress}",
                        deviceInfo.ProductType, ipAddress);
                    return;
                }

                // It's a HomeWizard device, add or update in database
                var existingDevice = await _dbContext.Devices
                    .FirstOrDefaultAsync(d => d.SerialNumber == deviceInfo.SerialNumber, cancellationToken);

                if (existingDevice == null)
                {
                    // New device
                    var device = new Device
                    {
                        Name = deviceInfo.ProductName,
                        IpAddress = ipAddress,
                        ProductType = productType,
                        ProductTypeRaw = deviceInfo.ProductType,
                        SerialNumber = deviceInfo.SerialNumber,
                        DiscoveredAt = DateTime.UtcNow,
                        LastSeenAt = DateTime.UtcNow,
                        IsEnabled = true
                    };

                    _dbContext.Devices.Add(device);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "Discovered new HomeWizard device: {ProductName} ({ProductType}) at {IpAddress}",
                        deviceInfo.ProductName, productType, ipAddress);
                }
                else
                {
                    // Update existing device
                    existingDevice.IpAddress = ipAddress;
                    existingDevice.LastSeenAt = DateTime.UtcNow;
                    existingDevice.ProductType = productType;
                    existingDevice.ProductTypeRaw = deviceInfo.ProductType;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "Updated existing HomeWizard device: {ProductName} ({ProductType}) at {IpAddress}",
                        deviceInfo.ProductName, productType, ipAddress);
                }
            }
            catch
            {
                // Not a HomeWizard device or not responding to API calls
            }
        }
        catch (Exception ex)
        {
            // Ignore discovery errors for individual devices
        }
    }
}