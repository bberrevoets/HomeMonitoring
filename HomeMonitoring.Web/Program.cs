using HomeMonitoring.SensorAgent.Data;
using HomeMonitoring.Web.Hubs;
using HomeMonitoring.Web.Services;
using HomeMonitoring.Web.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults
builder.AddServiceDefaults();

// Add PostgreSQL
builder.AddNpgsqlDbContext<SensorDbContext>("sensorsdb");

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

app.MapRazorPages();
app.MapHub<EnergyHub>("/energyHub");
app.MapDefaultEndpoints();

app.Run();