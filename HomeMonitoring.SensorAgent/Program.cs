using HomeMonitoring.SensorAgent;
using HomeMonitoring.SensorAgent.HealthChecks;
using HomeMonitoring.SensorAgent.Metrics;
using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

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
            options.IncludedData = IncludedData.TraceIdField |
                                   IncludedData.SpanIdField;
        })
        .WriteTo.Seq(
            builder.Configuration.GetConnectionString("seq") ?? "http://localhost:5341",
            apiKey: builder.Configuration["SeqApiKey"]));

    // Add the service defaults (e.g., logging, configuration, etc.)
    builder.AddServiceDefaults();

    builder.AddSqlServerDbContext<SensorDbContext>("sensorsdb");

    // Configure Email Settings
    builder.Services.Configure<EmailSettings>(options =>
    {
        var mailpitConnectionString = builder.Configuration.GetConnectionString("mailpit");
        if (!string.IsNullOrEmpty(mailpitConnectionString))
        {
            // Parse Aspire-provided connection string (format: "smtp://localhost:port")
            // Remove the "endpoint=" part
            var uriPart = mailpitConnectionString["endpoint=".Length..];

            // Parse with Uri
            var uri = new Uri(uriPart);
            options.SmtpHost = uri.Host;
            options.SmtpPort = uri.Port;
        }
        else
        {
            // Fallback to default values
            options.SmtpHost = "localhost";
            options.SmtpPort = 1025;
        }

        options.UseSsl = false;
        options.FromEmail = "homemonitoring@localhost";
        options.FromName = "Home Monitoring System";
        options.MonitoringEmail = builder.Configuration["Monitoring:Email"] ?? "admin@example.com";
        options.DeviceOfflineThresholdMinutes =
            builder.Configuration.GetValue("Monitoring:DeviceOfflineThresholdMinutes", 30);
    });

    // Register HTTP client
    builder.Services.AddHttpClient();

    // Register HomeWizard service
    builder.Services.AddScoped<IHomeWizardService, HomeWizardService>();

    // Register Email service
    builder.Services.AddScoped<IEmailService, EmailService>();

    // Register custom metrics as singleton
    builder.Services.AddSingleton<SensorAgentMetrics>();

    // Add additional health checks beyond the defaults
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("sensorsdb")!,
            name: "sql-server",
            tags: ["db", "ready"])
        .AddCheck<DeviceConnectivityHealthCheck>(
            "device-connectivity",
            tags: ["devices", "ready"]);

    // Register the Worker as a hosted service
    builder.Services.AddHostedService<Worker>();

    // Register the Device Monitoring service
    builder.Services.AddHostedService<DeviceMonitoringService>();

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