# HomeMonitoring

A comprehensive home energy monitoring system built with .NET Aspire that tracks and visualizes energy consumption from HomeWizard devices and controls Philips Hue lights.

## Overview

HomeMonitoring is a distributed application that consists of:
- **Web Dashboard**: Real-time energy monitoring interface with SignalR updates and Philips Hue light control
- **Sensor Agent**: Background service that polls HomeWizard devices, monitors device status, and manages Philips Hue lights
- **Migration Service**: Handles database migrations automatically
- **Infrastructure**: SQL Server database, Seq logging, and Mailpit for email notifications

## Features

- **Energy Monitoring**: Real-time tracking of HomeWizard devices (P1 Smart Meters, Energy Sockets)
- **Philips Hue Integration**: Control and monitor Philips Hue lights with real-time updates
- **Device Status Monitoring**: Automatic email alerts when devices go offline
- **Real-time Updates**: SignalR-powered live updates for energy data and light status
- **Toast Notifications**: User-friendly notifications for status changes
- **Dark/Light Theme Support**: Automatic theme switching with user preference persistence

## Prerequisites

- **.NET 10.0 SDK** or later
- **Visual Studio 2026** (version 18..0.0 Insiders or later) or **Visual Studio Code** with C# extension
- **Docker Desktop** (for running infrastructure services)
- **SQL Server LocalDB** (for local development without Docker)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/bberrevoets/HomeMonitoring.git
cd HomeMonitoring
```

### 2. Configure User Secrets

The application uses user secrets to store sensitive configuration. Initialize user secrets for each project:

```bash
# For the AppHost (Aspire orchestrator)
cd AppHost
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

```json
{
  "Monitoring": {
    "Email": "your-email@example.com",
    "DeviceOfflineThresholdMinutes": 30
  }
}
```

### 4. Run the Application

The easiest way to run the entire solution is using .NET Aspire:

```bash
# From the solution root directory
cd AppHost
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

```bash
# From the solution root directory
dotnet ef migrations add YourMigrationName -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
```

Or if you prefer to use the SensorAgent as the startup project:

```bash
dotnet ef migrations add YourMigrationName -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.SensorAgent\HomeMonitoring.SensorAgent.csproj
```

### Applying Migrations Manually

The Migration Service automatically applies migrations when the solution starts. However, you can apply them manually:

```bash
# Using the Web project as startup
dotnet ef database update -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj

# Or using the SensorAgent as startup
dotnet ef database update -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.SensorAgent\HomeMonitoring.SensorAgent.csproj
```

### Removing Migrations

If you need to remove the last migration:

```bash
dotnet ef migrations remove -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
```

## Adding HomeWizard Devices

1. Navigate to the Web Dashboard
2. Go to the "Devices" page
3. Click "Add Device"
4. Enter the device's IP address and a friendly name
5. The system will automatically detect the device type and start monitoring

Supported devices:
- **HWE-P1** (Smart Meter) - Monitor electricity and gas consumption
- **HWE-SKT** (Energy Socket) - Monitor individual appliance consumption

## Setting Up Philips Hue

1. Navigate to the Web Dashboard
2. Go to the "Lights" page
3. Click "Add Bridge"
4. Follow the on-screen instructions to:
   - Enter your Hue Bridge IP address
   - Press the button on your Hue Bridge
   - Complete the pairing process
5. Once connected, all lights will be automatically discovered and available for control

Features:
- **Real-time Control**: Toggle lights on/off and adjust brightness
- **Live Updates**: See changes from other sources (physical switches, other apps) in real-time
- **Smart Notifications**: Only get notified for changes made by others, not your own actions
- **Multiple Bridges**: Support for multiple Hue Bridges in one location

## Configuration Options

### SensorAgent Configuration

```json
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

```json
{
  "DashboardSettings": {
    "ChartDataMinutes": 10,
    "UpdateIntervalSeconds": 5,
    "DeviceOfflineThresholdMinutes": 30
  }
}
```

## Development

### System Requirements

- **.NET 10.0 SDK**: Download from [Microsoft .NET Downloads](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Visual Studio 2026** (18.0+): For full IDE experience with Aspire support
- **Docker Desktop**: Required for infrastructure services

### Running Without Aspire

You can run individual components separately for development:

1. **Start SQL Server** (using LocalDB or Docker):
```bash
   # Using Docker
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrongPassword123!" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

2. **Update connection strings** in `appsettings.json`:
```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=HomeMonitoring;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=true"
     }
   }
```

3. **Run migrations**:
```bash
   cd HomeMonitoring.MigrationService
   dotnet run
```

4. **Start services**:
```bash
   # Terminal 1: Web Dashboard
   cd HomeMonitoring.Web
   dotnet run

   # Terminal 2: Sensor Agent
   cd HomeMonitoring.SensorAgent
   dotnet run
```

### Installing EF Core Tools

If you don't have the EF Core tools installed:

```bash
dotnet tool install --global dotnet-ef
```

Update the tools:

```bash
dotnet tool update --global dotnet-ef
```

## Troubleshooting

### Migration Issues

If you encounter migration errors:

1. **Delete existing migrations**:
   - Remove the `Migrations` folder from `HomeMonitoring.Shared`
   
2. **Create fresh initial migration**:
```bash
   dotnet ef migrations add InitialCreate -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
```

3. **Check connection string**: Ensure the connection string in your configuration matches your SQL Server instance

### Device Connection Issues

- **HomeWizard devices**:
  - Verify the device IP address is correct
  - Ensure the device is on the same network
  - Check that the device's local API is enabled
  - Review logs in Seq for detailed error messages

- **Philips Hue Bridge**:
  - Ensure the bridge is connected to the same network
  - Verify you pressed the bridge button during pairing
  - Check bridge IP address hasn't changed
  - Review logs for authentication errors

### Email Notifications

- Check Mailpit UI at http://localhost:8025 to see sent emails
- Verify the monitoring email address is configured correctly
- Check the device offline threshold settings

### SignalR Connection Issues

- Check browser console for SignalR connection errors
- Verify that both Web and SensorAgent services are running
- Check firewall settings for websocket connections

## Architecture

```
HomeMonitoring/
├── AppHost/                    # .NET Aspire orchestrator
├── HomeMonitoring.Web/         # Razor Pages web dashboard
├── HomeMonitoring.SensorAgent/ # Background service for device polling
├── HomeMonitoring.MigrationService/ # Database migration runner
├── HomeMonitoring.Shared/      # Shared models and DbContext
└── ServiceDefaults/           # Common service configurations
```

### Key Components

- **SignalR Hubs**: Real-time communication for energy data and light updates
- **Background Services**: Device polling and monitoring services
- **Toast Notifications**: User-friendly notification system
- **Theme Management**: Dark/Light theme with user preference persistence
- **Entity Framework**: Database access with automatic migrations

## Technology Stack

- **.NET 10**: Latest .NET framework
- **ASP.NET Core**: Web framework
- **Razor Pages**: Server-side rendered UI
- **SignalR**: Real-time web functionality
- **Entity Framework Core**: Object-relational mapping
- **SQL Server**: Database
- **Bootstrap 5**: CSS framework with dark/light theme support
- **.NET Aspire**: Cloud-ready stack for distributed applications

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow C# coding conventions
- Use meaningful commit messages
- Add appropriate logging for new features
- Test both light and dark themes
- Ensure SignalR updates work correctly
- Add migrations for database changes

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Changelog

### Recent Updates
- **Philips Hue Integration**: Added complete light control and monitoring
- **Toast Notifications**: Replaced alert divs with professional toast system
- **Theme Support**: Added dark/light theme switching
- **Smart Notifications**: Suppress notifications for user-initiated changes
- **Real-time Updates**: Enhanced SignalR implementation for lights and energy data
- **.NET 10 Upgrade**: Updated to latest .NET framework