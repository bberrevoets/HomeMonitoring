using HomeMonitoring.SensorAgent;
using HomeMonitoring.SensorAgent.HealthChecks;
using HomeMonitoring.SensorAgent.Metrics;
using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Shared.Models;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using ServiceDefaults;

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
        .Enrich.WithProperty("Service", "HomeMonitoring.SensorAgent")
        .WriteTo.Console()
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
            // The Serilog OTLP sink doesn't read OTEL_SERVICE_NAME on its own; set service.name
            // explicitly so logs aren't tagged "unknown_service" in Grafana/Loki.
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = builder.Configuration["OTEL_SERVICE_NAME"] ?? "HomeMonitoring.SensorAgent"
            };
            options.IncludedData = IncludedData.TraceIdField |
                                   IncludedData.SpanIdField;
        })
        .WriteTo.Seq(
            builder.Configuration.GetConnectionString("seq") ?? "http://localhost:5341",
            apiKey: builder.Configuration["SeqApiKey"]));

    // Add the service defaults (e.g., logging, configuration, etc.)
    builder.AddServiceDefaults();

    builder.AddSqlServerDbContext<SensorDbContext>("sensorsdb");

    // Bind, override host/port from Aspire's mailpit connection string when present,
    // then validate. ValidateOnStart fails the host before any IHostedService runs.
    builder.Services
        .AddOptions<EmailSettings>()
        .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
        .PostConfigure(opts =>
        {
            var cs = builder.Configuration.GetConnectionString("mailpit");
            if (string.IsNullOrEmpty(cs)) return;

            // Aspire/CommunityToolkit connection strings come through as either
            // "Endpoint=smtp://host:port" (any casing) or a bare "smtp://host:port".
            var schemeIdx = cs.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx < 0) return;

            var equalsIdx = cs.LastIndexOf('=', schemeIdx - 1);
            var uriPart = equalsIdx >= 0 ? cs[(equalsIdx + 1)..] : cs;

            if (!Uri.TryCreate(uriPart, UriKind.Absolute, out var uri)) return;

            opts.SmtpHost = uri.Host;
            opts.SmtpPort = uri.Port;
        })
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Register HTTP client
    builder.Services.AddHttpClient();

    // Register HomeWizard service
    builder.Services.AddScoped<IHomeWizardService, HomeWizardService>();

    // Register Philips Hue service
    builder.Services.AddScoped<IPhilipsHueService, PhilipsHueService>();

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

    // Register the Hue Light Monitoring service
    builder.Services.AddHostedService<HueLightMonitoringService>();

    var host = builder.Build();
    host.Run();
}
catch (OptionsValidationException ex)
{
    foreach (var failure in ex.Failures)
        Log.Fatal("EmailSettings validation failed: {Failure}", failure);
    Log.Fatal("Stopping HomeMonitoring SensorAgent because email configuration is invalid.");
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}