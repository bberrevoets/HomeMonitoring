// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using HomeMonitoring.Shared.JsonConverters;

namespace HomeMonitoring.Shared.Models.HomeWizard;

public record P1MeterResponse : IEnergyDataResponse
{
    [JsonPropertyName("smr_version")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? SmrVersion { get; init; }

    [JsonPropertyName("meter_model")] public string? MeterModel { get; init; }

    [JsonPropertyName("unique_id")] public string? UniqueId { get; init; }

    [JsonPropertyName("active_tariff")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? ActiveTariff { get; init; }

    // Total power readings
    [JsonPropertyName("total_power_import_kwh")]
    public double? TotalPowerImportKWh { get; init; }

    [JsonPropertyName("total_power_import_t3_kwh")]
    public double? TotalPowerImportT3KWh { get; init; }

    [JsonPropertyName("total_power_import_t4_kwh")]
    public double? TotalPowerImportT4KWh { get; init; }

    [JsonPropertyName("total_power_export_kwh")]
    public double? TotalPowerExportKWh { get; init; }

    [JsonPropertyName("total_power_export_t3_kwh")]
    public double? TotalPowerExportT3KWh { get; init; }

    [JsonPropertyName("total_power_export_t4_kwh")]
    public double? TotalPowerExportT4KWh { get; init; }

    [JsonPropertyName("active_power_l1_w")]
    public double? ActivePowerL1W { get; init; }

    [JsonPropertyName("active_power_l2_w")]
    public double? ActivePowerL2W { get; init; }

    [JsonPropertyName("active_power_l3_w")]
    public double? ActivePowerL3W { get; init; }

    // Voltage
    [JsonPropertyName("active_voltage_l1_v")]
    public double? ActiveVoltageL1V { get; init; }

    [JsonPropertyName("active_voltage_l2_v")]
    public double? ActiveVoltageL2V { get; init; }

    [JsonPropertyName("active_voltage_l3_v")]
    public double? ActiveVoltageL3V { get; init; }

    // Current
    [JsonPropertyName("active_current_l1_a")]
    public double? ActiveCurrentL1A { get; init; }

    [JsonPropertyName("active_current_l2_a")]
    public double? ActiveCurrentL2A { get; init; }

    [JsonPropertyName("active_current_l3_a")]
    public double? ActiveCurrentL3A { get; init; }

    // Frequency
    [JsonPropertyName("active_frequency_hz")]
    public double? ActiveFrequencyHz { get; init; }

    // Power quality
    [JsonPropertyName("voltage_sag_l1_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? VoltageSagL1Count { get; init; }

    [JsonPropertyName("voltage_sag_l2_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? VoltageSagL2Count { get; init; }

    [JsonPropertyName("voltage_sag_l3_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? VoltageSagL3Count { get; init; }

    [JsonPropertyName("voltage_swell_l1_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? VoltageSwellL1Count { get; init; }

    [JsonPropertyName("voltage_swell_l2_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? VoltageSwellL2Count { get; init; }

    [JsonPropertyName("voltage_swell_l3_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? VoltageSwellL3Count { get; init; }

    [JsonPropertyName("any_power_fail_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? AnyPowerFailCount { get; init; }

    [JsonPropertyName("long_power_fail_count")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? LongPowerFailCount { get; init; }

    // Peak demand
    [JsonPropertyName("active_power_average_w")]
    public double? ActivePowerAverageW { get; init; }

    [JsonPropertyName("montly_power_peak_w")]
    public double? MonthlyPowerPeakW { get; init; }

    [JsonPropertyName("montly_power_peak_timestamp")]
    public long? MonthlyPowerPeakTimestamp { get; init; }

    [JsonPropertyName("gas_timestamp")] public long? GasTimestamp { get; init; }

    [JsonPropertyName("unique_gas_id")] public string? UniqueGasId { get; init; }

    // External meters
    [JsonPropertyName("external")] public object[]? External { get; init; }

    [JsonPropertyName("wifi_ssid")] public string? WifiSsid { get; init; }

    [JsonPropertyName("wifi_strength")]
    [JsonConverter(typeof(NullableIntFromNumberConverter))]
    public int? WifiStrength { get; init; }

    [JsonPropertyName("total_power_import_t1_kwh")]
    public double? TotalPowerImportT1KWh { get; init; }

    [JsonPropertyName("total_power_import_t2_kwh")]
    public double? TotalPowerImportT2KWh { get; init; }

    [JsonPropertyName("total_power_export_t1_kwh")]
    public double? TotalPowerExportT1KWh { get; init; }

    [JsonPropertyName("total_power_export_t2_kwh")]
    public double? TotalPowerExportT2KWh { get; init; }

    // Active power
    [JsonPropertyName("active_power_w")] public double? ActivePowerW { get; init; }

    // Gas
    [JsonPropertyName("total_gas_m3")] public double? TotalGasM3 { get; init; }
}