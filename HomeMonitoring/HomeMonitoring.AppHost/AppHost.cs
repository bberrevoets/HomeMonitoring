var builder = DistributedApplication.CreateBuilder(args);

// Add Seq for centralized logging with explicit endpoint
var seq = builder.AddSeq("seq")
    .WithDataVolume("homemonitoring-seq-server")
    .WithEnvironment("ACCEPT_EULA", "Y");

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("homemonitoring-postgres")
    .AddDatabase("sensorsdb");

// Add the SensorAgent worker service
var sensorAgent = builder.AddProject<Projects.HomeMonitoring_SensorAgent>("sensoragent")
    .WaitFor(seq)
    .WaitFor(postgres)
    .WithReference(seq)
    .WithReference(postgres);

builder.Build().Run();
