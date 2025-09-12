using HomeMonitoring.Shared.Data;
using HomeMonitoring.Web.Hubs;
using HomeMonitoring.Web.Models;
using HomeMonitoring.Web.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

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
        .Enrich.WithProperty("ServiceName", "HomeMonitoring.Web")
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

    // Add HttpClient for device communication
    builder.Services.AddHttpClient();

    // Add configuration
    builder.Services.Configure<DashboardSettings>(
        builder.Configuration.GetSection(DashboardSettings.SectionName));

    // Add additional health checks beyond the defaults
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("sensorsdb")!,
            name: "sql-server",
            tags: ["db", "ready"])
        .AddCheck("signalr", () => HealthCheckResult.Healthy("SignalR is operational"), ["signalr", "ready"]);

    // Add Health Checks UI - only monitoring this service
    builder.Services.AddHealthChecksUI(opt =>
        {
            opt.SetEvaluationTimeInSeconds(30); // Check every 30 seconds
            opt.MaximumHistoryEntriesPerEndpoint(60); // Keep 60 history entries
            opt.SetApiMaxActiveRequests(1);

            // Only add the current application health check endpoint
            opt.AddHealthCheckEndpoint("HomeMonitoring Web", "/health");

            // Note: In Aspire environments, each service manages its own health checks
            // Use the Aspire dashboard to monitor overall application health across services
        })
        .AddInMemoryStorage();

    // Add dashboard services
    builder.Services.AddScoped<IDashboardService, DashboardService>();
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

    // Map Health Checks UI
    app.MapHealthChecksUI(options =>
    {
        options.UIPath = "/health-ui"; // UI at /health-ui
        options.ApiPath = "/health-ui-api"; // API at /health-ui-api
    });

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