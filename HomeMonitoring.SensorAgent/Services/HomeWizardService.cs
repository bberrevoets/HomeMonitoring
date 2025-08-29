using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Models;
using HomeMonitoring.SensorAgent.Models.HomeWizard;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

namespace HomeMonitoring.SensorAgent.Services;

public class HomeWizardService : IHomeWizardService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SensorDbContext _dbContext;
    private readonly ILogger<HomeWizardService> _logger;

    public HomeWizardService(
        IHttpClientFactory httpClientFactory,
        SensorDbContext dbContext,
        ILogger<HomeWizardService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DeviceResponse> GetDeviceInfoAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetFromJsonAsync<DeviceResponse>(
                $"http://{ipAddress}/api", cancellationToken);

            if (response == null)
            {
                throw new Exception($"Failed to get device info from {ipAddress}");
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info from {IpAddress}", ipAddress);
            throw;
        }
    }

    public async Task<EnergyResponse> GetEnergyDataAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetFromJsonAsync<EnergyResponse>(
                $"http://{ipAddress}/api/v1/data", cancellationToken);

            if (response == null)
            {
                throw new Exception($"Failed to get energy data from {ipAddress}");
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting energy data from {IpAddress}", ipAddress);
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
        
        for (int i = 1; i <= 254; i++)
        {
            var ip = $"{networkPrefix}.{i}";
            var task = PingAndCheckDeviceAsync(ip, cancellationToken);
            discoveryTasks.Add(task);
        }

        await Task.WhenAll(discoveryTasks);
        _logger.LogInformation("HomeWizard device discovery completed");
    }

    private async Task<string?> GetLocalIpAddressAsync()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return null;
    }

    private async Task PingAndCheckDeviceAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            // First ping to check if device is online
            var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 100);
            
            if (reply.Status != IPStatus.Success)
            {
                return;
            }

            // Try to get device info to check if it's a HomeWizard device
            try
            {
                var deviceInfo = await GetDeviceInfoAsync(ipAddress, cancellationToken);
                
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
                        ProductType = deviceInfo.ProductType,
                        SerialNumber = deviceInfo.SerialNumber,
                        DiscoveredAt = DateTime.UtcNow,
                        LastSeenAt = DateTime.UtcNow,
                        IsEnabled = true
                    };

                    _dbContext.Devices.Add(device);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Discovered new HomeWizard device: {ProductName} at {IpAddress}", 
                        deviceInfo.ProductName, ipAddress);
                }
                else
                {
                    // Update existing device
                    existingDevice.IpAddress = ipAddress;
                    existingDevice.LastSeenAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Updated existing HomeWizard device: {ProductName} at {IpAddress}", 
                        deviceInfo.ProductName, ipAddress);
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