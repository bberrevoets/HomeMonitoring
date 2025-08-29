// ReSharper disable InconsistentNaming

namespace HomeMonitoring.SensorAgent.Models;

public enum HomeWizardProductType
{
    Unknown = 0,
    HWE_P1, // P1 Meter
    HWE_SKT, // Energy Socket
    HWE_WTR, // Water Meter
    HWE_KWH1, // kWh Meter (1-phase)
    HWE_KWH3, // kWh Meter (3-phase)
    SDM230_wifi, // SDM230 Meter
    SDM630_wifi, // SDM630 Meter
    HWE_DSP, // Display
    HWE_BAT // Battery
}