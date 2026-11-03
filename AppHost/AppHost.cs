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

// Aspire 13.6 publishes container ports on 127.0.0.1 only. Its HTTP health checks call
// http://localhost:<port>, which Windows tries on ::1 first and times out, so Seq and Mailpit stay
// Unhealthy and WaitFor(seq) / WaitFor(mailPit) never start the SensorAgent (and the web app after it).
// Proxied endpoints (the 13.4 behaviour) also listen on ::1.
var seq = builder.AddSeq("seq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("HomeMonitoring-Seq")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEndpoint("http", endpoint => endpoint.IsProxied = true, createIfNotExists: false);

var mailPit = builder.AddMailPit("mailpit")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("HomeMonitoring-MailPit")
    .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "true")
    .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "true")
    .WithEndpoint("http", endpoint => endpoint.IsProxied = true, createIfNotExists: false)
    .WithEndpoint("smtp", endpoint => endpoint.IsProxied = true, createIfNotExists: false);

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