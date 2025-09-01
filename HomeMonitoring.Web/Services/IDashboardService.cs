// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using HomeMonitoring.Web.Models;

namespace HomeMonitoring.Web.Services;

public interface IDashboardService
{
    Task<DashboardData> GetDashboardDataAsync();
    Task<List<ChartDataPoint>> GetDeviceChartDataAsync(int deviceId, int minutes = 10);
}