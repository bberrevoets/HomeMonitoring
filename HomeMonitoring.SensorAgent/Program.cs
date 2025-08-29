using HomeMonitoring.SensorAgent;
using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting HomeMonitoring SensorAgent");
    
    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "HomeMonitoring.SensorAgent")
        .WriteTo.Console()
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
            options.IncludedData = Serilog.Sinks.OpenTelemetry.IncludedData.TraceIdField |
                                   Serilog.Sinks.OpenTelemetry.IncludedData.SpanIdField;
        })
        .WriteTo.Seq(builder.Configuration.GetConnectionString("seq") ?? "http://localhost:5341"));

    // Add the service defaults (e.g., logging, configuration, etc.)
    builder.AddServiceDefaults();

    // Add PostgreSQL support
    builder.AddNpgsqlDataSource("sensorsdb");

    // Register DbContext
    builder.Services.AddDbContext<SensorDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("sensorsdb")));

    // Register HTTP client
    builder.Services.AddHttpClient();

    // Register HomeWizard service
    builder.Services.AddScoped<IHomeWizardService, HomeWizardService>();

    // Register the Worker as a hosted service
    builder.Services.AddHostedService<Worker>();

    // Ensure database is created and migrations are applied
    builder.Services.AddHostedService<DbInitializer>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
