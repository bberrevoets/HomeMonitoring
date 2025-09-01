// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace HomeMonitoring.Shared.Models.HomeWizard;

public interface IEnergyDataResponse
{
    string? WifiSsid { get; }
    int? WifiStrength { get; }
    double? ActivePowerW { get; }
    double? TotalPowerImportT1KWh { get; }
    double? TotalPowerImportT2KWh { get; }
    double? TotalPowerExportT1KWh { get; }
    double? TotalPowerExportT2KWh { get; }
    double? TotalGasM3 { get; }
}