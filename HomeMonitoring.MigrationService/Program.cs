// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using HomeMonitoring.MigrationService;
using HomeMonitoring.Shared.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.AddSqlServerDbContext<SensorDbContext>("sensorsdb");

var host = builder.Build();
host.Run();