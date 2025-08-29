using HomeMonitoring.SensorAgent;
using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.SensorAgent.Models;
using HomeMonitoring.SensorAgent.Services;
using Microsoft.EntityFrameworkCore;
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
        .WriteTo.Seq(builder.Configuration.GetConnectionString("seq") ?? "http://localhost:5341"));

    // Add the service defaults (e.g., logging, configuration, etc.)
    builder.AddServiceDefaults();

    // Add PostgreSQL support
    builder.AddNpgsqlDataSource("sensorsdb");

    // Register DbContext
    builder.Services.AddDbContext<SensorDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("sensorsdb")));

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

    // Register the Worker as a hosted service
    builder.Services.AddHostedService<Worker>();

    // Register the Device Monitoring service
    builder.Services.AddHostedService<DeviceMonitoringService>();

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