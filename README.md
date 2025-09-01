# HomeMonitoring

A comprehensive home energy monitoring system built with .NET Aspire that tracks and visualizes energy consumption from HomeWizard devices.

## Overview

HomeMonitoring is a distributed application that consists of:
- **Web Dashboard**: Real-time energy monitoring interface with SignalR updates
- **Sensor Agent**: Background service that polls HomeWizard devices and monitors device status
- **Migration Service**: Handles database migrations automatically
- **Infrastructure**: SQL Server database, Seq logging, and Mailpit for email notifications

## Prerequisites

- .NET 9.0 SDK or later
- Docker Desktop (for running infrastructure services)
- Visual Studio 2022 or VS Code with C# extension
- SQL Server LocalDB (for local development without Docker)

## Getting Started

### 1. Clone the Repository

```
git clone https://github.com/yourusername/HomeMonitoring.git
cd HomeMonitoring
```

### 2. Configure User Secrets

The application uses user secrets to store sensitive configuration. Initialize user secrets for each project:

```
# For the AppHost (Aspire orchestrator)
cd HomeMonitoring.AppHost
dotnet user-secrets init

# Set the SQL Server password
dotnet user-secrets set "Parameters:password" "YourStrongPassword123!"

# For the Web project (if needed for local development)
cd ../HomeMonitoring.Web
dotnet user-secrets init

# For the SensorAgent (if needed for local development)
cd ../HomeMonitoring.SensorAgent
dotnet user-secrets init
```

### 3. Configure Application Settings

Update `appsettings.json` in the SensorAgent project to set your monitoring email:

```
{
  "Monitoring": {
    "Email": "your-email@example.com",
    "DeviceOfflineThresholdMinutes": 30
  }
}
```

### 4. Run the Application

The easiest way to run the entire solution is using .NET Aspire:

```
# From the solution root directory
cd HomeMonitoring.AppHost
dotnet run
```

This will:
- Start SQL Server container with persistent storage
- Run database migrations automatically
- Start Seq for centralized logging
- Start Mailpit for email testing
- Launch the Sensor Agent service
- Launch the Web dashboard

Access the services at:
- **Aspire Dashboard**: https://localhost:17037
- **Web Dashboard**: https://localhost:7294
- **Seq Logs**: http://localhost:5341
- **Mailpit**: http://localhost:8025

## Database Migrations

### Creating a New Migration

When you make changes to the data models, create a new migration:

```
# From the solution root directory
dotnet ef migrations add YourMigrationName -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
```

Or if you prefer to use the SensorAgent as the startup project:

```
dotnet ef migrations add YourMigrationName -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.SensorAgent\HomeMonitoring.SensorAgent.csproj
```

### Applying Migrations Manually

The Migration Service automatically applies migrations when the solution starts. However, you can apply them manually:

```
# Using the Web project as startup
dotnet ef database update -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj

# Or using the SensorAgent as startup
dotnet ef database update -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.SensorAgent\HomeMonitoring.SensorAgent.csproj
```

### Removing Migrations

If you need to remove the last migration:

```
dotnet ef migrations remove -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
```

## Adding HomeWizard Devices

1. Navigate to the Web Dashboard
2. Go to the "Devices" page
3. Click "Add Device"
4. Enter the device's IP address and a friendly name
5. The system will automatically detect the device type and start monitoring

Supported devices:
- HWE-P1 (Smart Meter)
- HWE-SKT (Energy Socket)

## Configuration Options

### SensorAgent Configuration

```
{
  "Monitoring": {
    "Email": "admin@example.com",
    "DeviceOfflineThresholdMinutes": 30,
    "PollingIntervalSeconds": 5,
    "HealthCheckIntervalMinutes": 5
  }
}
```

### Web Dashboard Configuration

```
{
  "DashboardSettings": {
    "ChartDataMinutes": 10,
    "UpdateIntervalSeconds": 5,
    "DeviceOfflineThresholdMinutes": 30
  }
}
```

## Development

### Running Without Aspire

You can run individual components separately:

1. **Start SQL Server** (using LocalDB or Docker):
```
   # Using Docker
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrongPassword123!" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

2. **Update connection strings** in `appsettings.json`:
```
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=HomeMonitoring;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=true"
     }
   }
```

3. **Run migrations**:
```
   cd HomeMonitoring.MigrationService
   dotnet run
```

4. **Start services**:
```
   # Terminal 1: Web Dashboard
   cd HomeMonitoring.Web
   dotnet run

   # Terminal 2: Sensor Agent
   cd HomeMonitoring.SensorAgent
   dotnet run
```

### Installing EF Core Tools

If you don't have the EF Core tools installed:

```
dotnet tool install --global dotnet-ef
```

Update the tools:

```
dotnet tool update --global dotnet-ef
```

## Troubleshooting

### Migration Issues

If you encounter migration errors:

1. **Delete existing migrations**:
   - Remove the `Migrations` folder from `HomeMonitoring.Shared`
   
2. **Create fresh initial migration**:
```
   dotnet ef migrations add InitialCreate -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
```

3. **Check connection string**: Ensure the connection string in your configuration matches your SQL Server instance

### Device Connection Issues

- Verify the device IP address is correct
- Ensure the device is on the same network
- Check that the device's local API is enabled
- Review logs in Seq for detailed error messages

### Email Notifications

- Check Mailpit UI at http://localhost:8025 to see sent emails
- Verify the monitoring email address is configured correctly
- Check the device offline threshold settings

## Architecture

```
HomeMonitoring/
??? HomeMonitoring.AppHost/     # .NET Aspire orchestrator
??? HomeMonitoring.Web/         # Razor Pages web dashboard
??? HomeMonitoring.SensorAgent/ # Background service for device polling
??? HomeMonitoring.MigrationService/ # Database migration runner
??? HomeMonitoring.Shared/      # Shared models and DbContext
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.