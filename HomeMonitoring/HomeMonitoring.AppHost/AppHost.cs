using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var mailpit = builder.AddMailPit("mailpit")
    .WithDataVolume("homemonitoring-mailpit");

// Add Seq for centralized logging with explicit endpoint
var seq = builder.AddSeq("seq")
    .WithDataVolume("homemonitoring-seq-server")
    .WithEnvironment("ACCEPT_EULA", "Y");

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithPgWeb()
    .WithDataVolume("homemonitoring-postgres")
    .AddDatabase("sensorsdb");

// Add the SensorAgent worker service
var sensorAgent = builder.AddProject<HomeMonitoring_SensorAgent>("sensoragent")
    .WaitFor(seq)
    .WaitFor(postgres)
    .WithReference(seq)
    .WithReference(postgres)
    .WithReference(mailpit)
    .WaitFor(mailpit);

// Add the Web application
var web = builder.AddProject<HomeMonitoring_Web>("web")
    .WaitFor(seq)
    .WaitFor(postgres)
    .WaitFor(sensorAgent)
    .WithReference(seq)
    .WithReference(postgres)
    .WithReference(mailpit)
    .WaitFor(mailpit);

builder.Build().Run();