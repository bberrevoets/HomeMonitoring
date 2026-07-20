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
unless running services outside Aspire. In production the same runner executes as a `systemd`
one-shot before the apps start; see [deploy/README.md](deploy/README.md).

## Architecture

Five .NET 10 projects wired together by Aspire:

- [AppHost](AppHost/AppHost.cs) — Aspire orchestrator. Declares dependencies, lifetimes, and `WaitFor`
  ordering: SQL → MigrationService (WaitForCompletion) → SensorAgent → Web.
- [ServiceDefaults](ServiceDefaults/Extensions.cs) — `AddServiceDefaults()` adds OpenTelemetry
  (metrics + tracing, custom meters `HomeMonitoring.SensorAgent` and `HomeMonitoring.Web`),
  service discovery, resilient HttpClient, and `/health` + `/alive` endpoints (Development only).
  Sets the OTel resource `service.name` explicitly (falling back to the application name when
  Aspire's `OTEL_SERVICE_NAME` is absent — e.g. running standalone against an external OTLP stack)
  so telemetry is never reported as `unknown_service`.
- [HomeMonitoring.Shared](HomeMonitoring.Shared/) — `SensorDbContext`, EF migrations, domain models
  (`Device`, `EnergyReading`, `HueLight`, `HueLightReading`, `HueBridgeConfiguration`), and
  HomeWizard/Hue DTOs. Referenced by Web, SensorAgent, MigrationService.
- [HomeMonitoring.SensorAgent](HomeMonitoring.SensorAgent/) — three hosted services:
  - `Worker` — polls HomeWizard devices every 10s, writes `EnergyReading` rows, updates `Device.LastSeenAt`.
  - `DeviceMonitoringService` — sends email alerts (via Mailpit in dev) when devices exceed
    `Email:DeviceOfflineThresholdMinutes`.
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
  - **Theming**: dark/light mode uses Bootstrap 5.3's `data-bs-theme` attribute on `<html>`, persisted
    in `localStorage` (key `theme`). The stored theme is applied by an **inline, render-blocking script
    in the `_Layout.cshtml` `<head>`** so `data-bs-theme` exists before first paint — this prevents a
    flash-of-light-theme (FOUC) on every full-page navigation. `wwwroot/js/theme-toggle.js` (loaded at
    the end of `<body>`) only owns the toggle button and icon/label syncing. **Keep the head snippet** —
    moving theme initialization back to `DOMContentLoaded` reintroduces the flash.

### Cross-cutting

- **Connection strings** flow from Aspire: `sensorsdb` (SQL Server), `seq`, `mailpit`. The Mailpit
  connection string has form `endpoint=smtp://host:port` and is parsed manually in
  [SensorAgent Program.cs](HomeMonitoring.SensorAgent/Program.cs).
- **Logging**: Serilog bootstrap logger → reconfigured from config, enriched with
  `Service` property (`HomeMonitoring.Web` / `HomeMonitoring.SensorAgent`), shipped to Seq and OTLP.
  Aspire's OTLP endpoint is in `OTEL_EXPORTER_OTLP_ENDPOINT`. The Serilog OTLP sink also sets the
  `service.name` resource attribute (it does not read `OTEL_SERVICE_NAME` on its own).
- **Health checks**: each service adds `sql-server` (tagged `db`,`ready`) plus service-specific checks
  (`signalr` on Web, `device-connectivity` on SensorAgent). Endpoints are Dev-only.

## Conventions

- Configuration sections used in code: `DashboardSettings` (Web); on the SensorAgent the `Email`
  section (bound to `EmailSettings`, **validated at startup** — `SmtpHost`/`SmtpPort` are filled
  from the Aspire `mailpit` connection string) plus `Monitoring:PollingIntervalSeconds` and
  `Monitoring:HealthCheckIntervalMinutes`.
- **Secrets convention**: each app has a single committed `appsettings.json` where every secret value
  is the literal marker `"InSecrets"` (`ConnectionStrings:sensorsdb`/`seq`/`mailpit`, `SeqApiKey`,
  `Email:SmtpUsername`/`SmtpPassword`, `OTEL_EXPORTER_OTLP_ENDPOINT`). Real dev values come from **User Secrets** (loaded only in the
  Development environment; Aspire also injects the `ConnectionStrings__*` env vars, which override the
  markers). Do **not** add `appsettings.Development.json`/`appsettings.Production.json` — they are
  gitignored. In production the deploy substitutes the markers (see below).
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

## Production deployment (Linux/systemd)

Production does **not** use Aspire. The three services are published **self-contained for
`linux-arm64`** and run as `systemd` units (`HomeMonitoringMigration` one-shot →
`HomeMonitoringSensorAgent` + `HomeMonitoringDashboard`), mirroring Aspire's migrate-then-start
ordering via a `Before=` on the migration unit plus `After=`/`Requires=` drop-ins on the apps
(so the apps don't start if migrations fail). Deployment runs from the **Deploy to on-prem** GitHub
Actions workflow ([.github/workflows/deploy.yml](.github/workflows/deploy.yml)) on a **self-hosted
runner installed on the box** (the target is on a private LAN): it publishes, replaces each
`"InSecrets"` marker in the published `appsettings.json` with the matching **GitHub Actions Secret**
(config-key path joined with `__`) via
[.github/scripts/replace-secrets.py](.github/scripts/replace-secrets.py), packages the tarballs, and
calls `deploy.sh` locally. `deploy.sh` ships the tokenized `appsettings.json` (chmod 600) — there is
no `appsettings.Production.json`. Because the deploy **overwrites** `appsettings.json`, the dashboard's `ASPNETCORE_URLS` listen
address (Kestrel binds `localhost` only without it, and it can't be dev-safely committed) lives in a
systemd drop-in
([HomeMonitoringDashboard.service.d/20-environment.conf](deploy/systemd/HomeMonitoringDashboard.service.d/20-environment.conf)),
**not** in `appsettings.json` — and `deploy.sh` re-creates that drop-in if it goes missing (via the
`hm-reconcile-units` root helper, which synthesizes fixed content and never overwrites a present
drop-in), then verifies at runtime that Kestrel bound `0.0.0.0:5000` and fails the deploy if not.
Deploy artifacts (unit files + `deploy.sh`) are versioned in
[deploy/](deploy/README.md), the source of truth for the server layout, secret names, and
deploy/rollback procedure.
