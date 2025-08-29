using Microsoft.EntityFrameworkCore;

namespace HomeMonitoring.SensorAgent.Data;

public class DbInitializer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        IServiceProvider serviceProvider,
        ILogger<DbInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for dependencies to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SensorDbContext>();

        _logger.LogInformation("Ensuring database is created...");
        
        try
        {
            await dbContext.Database.EnsureCreatedAsync(stoppingToken);
            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the database");
        }
        
        // This is a one-time initialization service, so we can stop after it's done
    }
}