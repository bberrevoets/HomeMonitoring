using System.Text.Json.Serialization;
using HomeMonitoring.SensorAgent.JsonConverters;

namespace HomeMonitoring.SensorAgent.Models.HomeWizard;

public record SocketResponse : IEnergyDataResponse
{
    [JsonPropertyName("wifi_ssid")] 
    public string? WifiSsid { get; init; }

    [JsonPropertyName("wifi_strength")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? WifiStrength { get; init; }

    [JsonPropertyName("total_power_import_kwh")]
    public double? TotalPowerImportKWh { get; init; }

    [JsonPropertyName("total_power_import_t1_kwh")]
    public double? TotalPowerImportT1KWh { get; init; }

    [JsonPropertyName("total_power_export_kwh")]
    public double? TotalPowerExportKWh { get; init; }

    [JsonPropertyName("total_power_export_t1_kwh")]
    public double? TotalPowerExportT1KWh { get; init; }

    [JsonPropertyName("active_power_w")] 
    public double? ActivePowerW { get; init; }

    [JsonPropertyName("active_power_l1_w")]
    public double? ActivePowerL1W { get; init; }

    [JsonPropertyName("active_voltage_v")] 
    public double? ActiveVoltageV { get; init; }

    [JsonPropertyName("active_current_a")] 
    public double? ActiveCurrentA { get; init; }

    [JsonPropertyName("active_reactive_power_var")]
    public double? ActiveReactivePowerVar { get; init; }

    [JsonPropertyName("active_apparent_power_va")]
    public double? ActiveApparentPowerVa { get; init; }

    [JsonPropertyName("active_power_factor")]
    public double? ActivePowerFactor { get; init; }

    [JsonPropertyName("active_frequency_hz")]
    public double? ActiveFrequencyHz { get; init; }
    
    // Interface implementation - Socket doesn't have T2 tariffs or gas
    public double? TotalPowerImportT2KWh => null;
    public double? TotalPowerExportT2KWh => null;
    public double? TotalGasM3 => null;
}