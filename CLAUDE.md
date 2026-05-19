# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

The whole distributed app is orchestrated by .NET Aspire from [AppHost](AppHost/). Run it with:

```powershell
dotnet run --project AppHost
```

`AppHost` is the only project you normally run; it spins up a SQL Server container, Seq, Mailpit,
runs `HomeMonitoring.MigrationService` to completion, then starts `HomeMonitoring.SensorAgent`
and `HomeMonitoring.Web`. Ports are dynamic — open the Aspire dashboard URL printed to the console.

Build / test individual projects:

```powershell
dotnet build HomeMonitoring.slnx
dotnet run --project HomeMonitoring.Web        # standalone web (needs external SQL + Seq)
dotnet run --project HomeMonitoring.SensorAgent
```

The user-secret `Parameters:password` on `AppHost` is required (SQL Server SA password) —
see [README.md](README.md) §"Configure User Secrets".

## EF Core Migrations

`HomeMonitoring.Shared` owns the `DbContext`; either web or sensor agent can serve as startup project.
Use a design-time factory ([DesignTimeDbContextFactory.cs](HomeMonitoring.Shared/Data/DesignTimeDbContextFactory.cs)).

```powershell
dotnet ef migrations add <Name> -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
dotnet ef migrations remove   -p .\HomeMonitoring.Shared\HomeMonitoring.Shared.csproj -s .\HomeMonitoring.Web\HomeMonitoring.Web.csproj
```

`MigrationService` applies migrations automatically at startup — do not call `dotnet ef database update`
unless running services outside Aspire.

## Architecture

Five .NET 10 projects wired together by Aspire:

- [AppHost](AppHost/AppHost.cs) — Aspire orchestrator. Declares dependencies, lifetimes, and `WaitFor`
  ordering: SQL → MigrationService (WaitForCompletion) → SensorAgent → Web.
- [ServiceDefaults](ServiceDefaults/Extensions.cs) — `AddServiceDefaults()` adds OpenTelemetry
  (metrics + tracing, custom meters `HomeMonitoring.SensorAgent` and `HomeMonitoring.Web`),
  service discovery, resilient HttpClient, and `/health` + `/alive` endpoints (Development only).
- [HomeMonitoring.Shared](HomeMonitoring.Shared/) — `SensorDbContext`, EF migrations, domain models
  (`Device`, `EnergyReading`, `HueLight`, `HueLightReading`, `HueBridgeConfiguration`), and
  HomeWizard/Hue DTOs. Referenced by Web, SensorAgent, MigrationService.
- [HomeMonitoring.SensorAgent](HomeMonitoring.SensorAgent/) — three hosted services:
  - `Worker` — polls HomeWizard devices every 10s, writes `EnergyReading` rows, updates `Device.LastSeenAt`.
  - `DeviceMonitoringService` — sends email alerts (via Mailpit in dev) when devices exceed
    `Monitoring:DeviceOfflineThresholdMinutes`.
  - `HueLightMonitoringService` — polls Hue bridges, persists light state.
  - Exposes services consumed cross-project: `IHomeWizardService`, `IPhilipsHueService`, `IEmailService`,
    plus `SensorAgentMetrics` (singleton meter) and `DeviceConnectivityHealthCheck`.
- [HomeMonitoring.Web](HomeMonitoring.Web/) — Razor Pages dashboard with two SignalR hubs:
  - `EnergyHub` (`/energyHub`) — pushed by `DashboardUpdateService` (hosted).
  - `LightsHub` (`/lightsHub`) — pushed by `PhilipsHueLightMonitorService` (hosted, web-side, polls Hue
    every 2s and diffs against an in-memory `_previousStates` map so users don't get toast-notified
    about their own actions).
  - Note: Web references `HomeMonitoring.SensorAgent` to reuse `IPhilipsHueService` — the Web project
    instantiates the same Hue service classes directly, it does **not** call SensorAgent over HTTP.

### Cross-cutting

- **Connection strings** flow from Aspire: `sensorsdb` (SQL Server), `seq`, `mailpit`. The Mailpit
  connection string has form `endpoint=smtp://host:port` and is parsed manually in
  [SensorAgent Program.cs](HomeMonitoring.SensorAgent/Program.cs).
- **Logging**: Serilog bootstrap logger → reconfigured from config, enriched with
  `Service` property (`HomeMonitoring.Web` / `HomeMonitoring.SensorAgent`), shipped to Seq and OTLP.
  Aspire's OTLP endpoint is in `OTEL_EXPORTER_OTLP_ENDPOINT`.
- **Health checks**: each service adds `sql-server` (tagged `db`,`ready`) plus service-specific checks
  (`signalr` on Web, `device-connectivity` on SensorAgent). Endpoints are Dev-only.

## Conventions

- Configuration sections used in code: `DashboardSettings` (Web), `Monitoring:Email`,
  `Monitoring:DeviceOfflineThresholdMinutes`, `Monitoring:PollingIntervalSeconds`,
  `Monitoring:HealthCheckIntervalMinutes` (SensorAgent).
- `Device.ProductType` (enum `HomeWizardProductType`) is persisted as **string** (see `OnModelCreating`).
  When polling, the `Worker` skips any product type other than `HWE_P1` or `HWE_SKT` and disables a
  device on `NotSupportedException` — don't add new product types without updating the switch in
  [HomeWizardService](HomeMonitoring.SensorAgent/Services/HomeWizardService.cs).
- Device-poll loops swallow `TaskCanceledException` / `HttpRequestException` at `Debug` level on
  purpose — those mean "device offline", which `DeviceMonitoringService` handles separately. Don't
  promote them to warnings.
- Stale build artifacts exist under `./HomeMonitoring/HomeMonitoring.AppHost` and
  `./HomeMonitoring/HomeMonitoring.ServiceDefaults` (leftover from a rename). Ignore — actual sources
  are at the repo root.
