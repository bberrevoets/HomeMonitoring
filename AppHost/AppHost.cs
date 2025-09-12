using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("password", true);

var sqlServer = builder.AddSqlServer("sqlserver", password)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("HomeMonitoring-SQL");

var db = sqlServer.AddDatabase("sensorsdb");

var migration = builder.AddProject<HomeMonitoring_MigrationService>("HomeMonitoring-MigrationService")
    .WithReference(db)
    .WaitFor(db);

var seq = builder.AddSeq("seq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("HomeMonitoring-Seq")
    .WithEnvironment("ACCEPT_EULA", "Y");

var mailPit = builder.AddMailPit("mailpit")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("HomeMonitoring-MailPit");

var sensorAgent = builder.AddProject<HomeMonitoring_SensorAgent>("HomeMonitoring-SensorAgent")
    .WaitFor(seq)
    .WithReference(db)
    .WithReference(seq)
    .WithReference(mailPit)
    .WaitFor(mailPit)
    .WaitForCompletion(migration);

_ = builder.AddProject<HomeMonitoring_Web>("web")
    .WithReference(db)
    .WithReference(seq)
    .WaitFor(sensorAgent);

builder.Build().Run();