using HomeMonitoring.MigrationService;
using HomeMonitoring.Shared.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.AddSqlServerDbContext<SensorDbContext>("sensorsdb");

var host = builder.Build();
host.Run();