# AGENTS.md

Instructions for AI coding agents (Claude Code, Codex and others) working in this repository. `CLAUDE.md` only imports
this file, so edit this one. [README.md](README.md) describes the project for people; this file lists what the code
alone does not tell you.

<!-- repo-init:start -->
## Repository Info

- **Company:** Berrevoets Systems
- **Author:** Bert Berrevoets
- **Contact:** <bert@berrevoets.net>
- **Remote:** <https://github.com/bberrevoets/HomeMonitoring> (public)

## Workflow Rules

- **GitHub Flow:** `main` is the default and only long-lived branch. It is protected by the ruleset "Protect main":
  no direct pushes, no deletion, changes only through a pull request, and all PR conversations must be resolved
  before merging. Merged head branches are deleted automatically.
- After cloning, run `git config pull.rebase true` once, so `git pull` rebases instead of creating merge commits.
- Work on a branch per topic, push it, and open a PR to `main` only when Bert asks for it, assigned to him
  (`gh pr create --assignee bberrevoets`). PRs are squash-merged. After a merge: switch to `main` and pull, then delete the
  local branch with `git branch -D <branch>` (`-d` refuses after a squash merge) once
  `gh pr view <branch> --json state,headRefOid` shows `MERGED` and the same commit as `git rev-parse <branch>`.
- **"Update the documentation"** means `AGENTS.md`, the project's own `README.md` files (not vendored third-party ones)
  and `CHANGELOG.md`; then commit and push.
- [CHANGELOG.md](CHANGELOG.md) follows Keep a Changelog: the Attributors list at the top, and one `Author: *Name*`
  line under each section heading (`### Added`, `### Changed`, ...).
- Markdown must pass `markdownlint-cli2 "**/*.md"` with [.markdownlint.json](.markdownlint.json) (180-character
  lines) before every commit.
- Binaries go through Git LFS: every `filter=lfs` pattern in `.gitattributes` — the standard set (PDF, ZIP, PNG,
  JPEG, STEP/STP, STL, 3MF, F3D/F3Z) plus any pattern this repository adds. Add a pattern there before committing
  another binary type (GIF, BMP, ...). The patterns are lowercase and match `X.PNG` only where `core.ignorecase` is
  true (Windows, macOS); on a case-sensitive system check a new binary with `git check-attr filter -- <file>` before
  committing it.
- Commit messages use the conventional style (`feat:`, `fix:`, `docs:`, `chore:`).
<!-- repo-init:end -->

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
  - `Worker` — every 10s polls all enabled HomeWizard devices **concurrently** (one DI scope +
    `DbContext` per device, so a slow/unreachable device can't stall the others), writes
    `EnergyReading` rows, updates `Device.LastSeenAt`, and persists last-known status on the `Device`
    (WiFi SSID/strength every poll — it's free in the energy response; firmware/API a few times a day).
  - `DeviceMonitoringService` — sends email alerts (via Mailpit in dev) when devices exceed
    `Email:DeviceOfflineThresholdMinutes`. Offline detection is suppressed during a short startup-grace
    window so a restart's stale `LastSeenAt` doesn't fire a false alert before the `Worker` re-polls.
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
  - **Devices pages** (`Pages/Devices/`): `Index` lists devices with per-row **Edit** / **Details** /
    **Delete**. `Details` is read-only and shows the device's last-known status (firmware, WiFi, current
    power) read **from the DB** — it does **not** contact the device (HomeWizard sockets accept ~one
    client connection, held by the SensorAgent). **Delete** is a confirmation modal on `Index` backed by
    `OnPostDelete`: it first sets `IsEnabled = false` (polling stops within one cycle), then removes the
    `EnergyReading` rows in batches of 5000 (`ExecuteDeleteAsync` with `Take`), then the device. **Don't go back
    to the single cascade delete**: for a device with months of readings it runs past SQL Server's 30 s command
    timeout on the remote database and the device stays (2026-11-10). An interrupted delete leaves the device
    disabled; clicking Delete again continues. `Index` and `Details` carry
    `[ResponseCache(NoStore=…)]` so volatile Last Seen / reading counts aren't served from browser cache.
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
  `Service` property (`HomeMonitoring.Web` / `HomeMonitoring.SensorAgent`), shipped to the console and
  **directly to Seq only**. There is deliberately no Serilog OTLP sink (removed 2026-11-10): in production the
  laptop's OTel collector forwards OTLP logs to Seq too, so every event was stored twice, the second copy
  without its message template. Traces and metrics still go over OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`, via
  `AddServiceDefaults`). `AddSerilog` replaces the logging providers, so the `Logging:LogLevel` sections in
  `appsettings.json` do nothing: levels live in the `Serilog:MinimumLevel` section of **both** apps, with
  `Microsoft` and `System.Net.Http.HttpClient` at `Warning`. Without the HttpClient override every poll logs four
  Information lines, a dead device adds "HTTP request failed" with its exception (~100,000 events a day for three
  retired sockets), and the Hue bridge URL — which contains the Hue API key — ends up in Seq.
- **Health checks**: each service adds `sql-server` (tagged `db`,`ready`) plus service-specific checks
  (`signalr` on Web, `device-connectivity` on SensorAgent). Endpoints are Dev-only.
- **HTTP clients for LAN polling**: `AddServiceDefaults()` attaches the standard resilience handler to
  every `HttpClient`. HomeWizard (`HomeWizardService.HttpClientName`) and Hue
  (`PhilipsHueService.HttpClientName`) instead use dedicated named clients registered with
  `RemoveAllResilienceHandlers()` — for expected-offline LAN devices, retries/circuit-breaker only add
  Warning-level log spam and can trip and fail unrelated devices; a failed poll is a single fast attempt.
  Only the SensorAgent contacts the HomeWizard devices — the sockets accept ~one connection at a time,
  so the Web reads device status from the DB (see the Devices `Details` page) rather than polling them.
  (Corollary: don't run a second poller — e.g. a production instance — against the same physical devices;
  the two will fight over that one connection.)

## Conventions

- **Aspire 13.6: Seq and Mailpit endpoints are proxied on purpose** (`IsProxied = true` in `AppHost.cs`). 13.6
  publishes container ports on `127.0.0.1` only; their HTTP health checks call `http://localhost:<port>`, which
  Windows tries on `::1` first and times out, so the resources stay Unhealthy and `WaitFor(seq)` /
  `WaitFor(mailPit)` never start the SensorAgent and the web app. Do not remove the proxy settings.
- Configuration sections used in code: `DashboardSettings` (Web); on the SensorAgent the `Email`
  section (bound to `EmailSettings`, **validated at startup** — `SmtpHost`/`SmtpPort` are filled
  from the Aspire `mailpit` connection string) plus `Monitoring:HealthCheckIntervalMinutes`. The HomeWizard poll
  interval is a hardcoded 10 s (`Worker.cs`, `_pollingInterval`); `Monitoring:PollingIntervalSeconds` in
  `appsettings.json` is not read anywhere.
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
- `Device.Name` is a **user-editable** friendly name, changed from the `Devices/Edit` page
  ([Edit.cshtml.cs](HomeMonitoring.Web/Pages/Devices/Edit.cshtml.cs)). It is set only at device
  creation (`Create` page / `HomeWizardService` discovery); the polling `Worker` and discovery's
  update path deliberately **never reassign `Name`** on an existing device, so a user rename survives.
  Keep it that way — don't add polling/discovery code that overwrites `Name`.
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
