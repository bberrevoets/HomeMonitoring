// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("password", true);

var sqlServer = builder.AddSqlServer("sqlserver", password, 1433)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("homemonitoring-sql-server");

var db = sqlServer.AddDatabase("sensorsdb");

var migrated = builder.AddProject<HomeMonitoring_MigrationService>("homemonitoring-migrationservice")
    .WithReference(db)
    .WaitFor(db);

// Add Seq for centralized logging with explicit endpoint
var seq = builder.AddSeq("seq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("homemonitoring-seq-server")
    .WithEnvironment("ACCEPT_EULA", "Y");

var mailpit = builder.AddMailPit("mailpit")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("homemonitoring-mailpit");

// Add the SensorAgent worker service
var sensorAgent = builder.AddProject<HomeMonitoring_SensorAgent>("sensoragent")
    .WaitFor(seq)
    .WithReference(db)
    .WithReference(seq)
    .WithReference(mailpit)
    .WaitFor(mailpit)
    .WaitForCompletion(migrated);

// Add the Web application
var web = builder.AddProject<HomeMonitoring_Web>("web")
    .WithReference(db)
    .WithReference(seq)
    .WaitFor(sensorAgent);

builder.Build().Run();