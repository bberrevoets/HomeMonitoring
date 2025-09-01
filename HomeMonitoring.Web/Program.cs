// Copyright (c) 2025 Bert Berrevoets
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using HomeMonitoring.Shared.Data;
using HomeMonitoring.Web.Hubs;
using HomeMonitoring.Web.Models;
using HomeMonitoring.Web.Services;
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
            serverUrl: builder.Configuration.GetConnectionString("seq") ?? "http://localhost:5341",
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