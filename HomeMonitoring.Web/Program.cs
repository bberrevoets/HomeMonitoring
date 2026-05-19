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