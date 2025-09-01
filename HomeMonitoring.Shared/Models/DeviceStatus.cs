// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace HomeMonitoring.Shared.Models;

public class DeviceStatus
{
    public int DeviceId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastOfflineAlertSent { get; set; }
    public DateTime? WentOfflineAt { get; set; }
}