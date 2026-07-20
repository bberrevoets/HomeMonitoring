using HomeMonitoring.SensorAgent.Services;
using HomeMonitoring.Shared.Data;
using HomeMonitoring.Web.Hubs;
using HomeMonitoring.Web.Models;
using HomeMonitoring.Web.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using ServiceDefaults;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting HomeMonitoring Web");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "HomeMonitoring.Web")
        .WriteTo.Console()
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
            // The Serilog OTLP sink doesn't read OTEL_SERVICE_NAME on its own; set service.name
            // explicitly so logs aren't tagged "unknown_service" in Grafana/Loki.
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = builder.Configuration["OTEL_SERVICE_NAME"] ?? "HomeMonitoring.Web"
            };
            options.IncludedData = IncludedData.TraceIdField |
                                   IncludedData.SpanIdField;
        })
        .WriteTo.Seq(
            builder.Configuration.GetConnectionString("seq") ?? "http://localhost:5341",
            apiKey: builder.Configuration["SeqApiKey"]));

    // Add service defaults
    builder.AddServiceDefaults();

    builder.AddSqlServerDbContext<SensorDbContext>("sensorsdb");

    // Add Razor Pages
    builder.Services.AddRazorPages();

    // Add SignalR
    builder.Services.AddSignalR();
    builder.Services.AddHostedService<PhilipsHueLightMonitorService>();

    // Add HttpClient for device communication
    builder.Services.AddHttpClient();

    // Dedicated client for the Details page's live HomeWizard fetch. AddServiceDefaults adds the
    // standard resilience handler to every client; for LAN device polling that means retries + Warning
    // spam on an (expected) offline device. Strip it so an unreachable device fails fast and the page
    // shows its "unreachable" state immediately. Mirrors the SensorAgent registration.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental
    builder.Services.AddHttpClient(HomeWizardService.HttpClientName)
        .RemoveAllResilienceHandlers();
    // The dashboard polls the Hue bridge every 2s; strip resilience so Polly doesn't log a
    // per-attempt telemetry line for every local call.
    builder.Services.AddHttpClient(PhilipsHueService.HttpClientName)
        .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

    // Add configuration
    builder.Services.Configure<DashboardSettings>(
        builder.Configuration.GetSection(DashboardSettings.SectionName));

    // Health checks beyond the Aspire defaults; aggregated view lives in the Aspire dashboard.
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("sensorsdb")!,
            name: "sql-server",
            tags: ["db", "ready"])
        .AddCheck("signalr", () => HealthCheckResult.Healthy("SignalR is operational"), ["signalr", "ready"]);

    // Add dashboard services
    builder.Services.AddScoped<IDashboardService, DashboardService>();
    builder.Services.AddScoped<IPhilipsHueService, PhilipsHueService>();
    // Details page fetches live device info (firmware, WiFi, live power) on demand.
    builder.Services.AddScoped<IHomeWizardService, HomeWizardService>();
    builder.Services.AddHostedService<DashboardUpdateService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthorization();

    // Add Serilog request logging
    app.UseSerilogRequestLogging();

    app.MapRazorPages();
    app.MapHub<EnergyHub>("/energyHub");
    app.MapDefaultEndpoints();
    app.MapHub<LightsHub>("/lightsHub");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}