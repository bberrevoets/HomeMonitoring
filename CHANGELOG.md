# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Attributors

- Bert Berrevoets (B.N. Berrevoets)
- dependabot[bot]
- Claude Opus 4.7 and Claude Opus 4.8 — co-authors of the 2026-07 work (deploy tooling, device pages, fixes)
- Claude Opus 5.5 — co-author of the 2026-11 AI agent setup

## [Unreleased]

### Added

Author: *Bert Berrevoets*

- View a device's full details from the **Devices** page: a new `Devices/Details` page (wired to the
  per-row **Details** button) shows the device's last-known status — firmware, API version, WiFi
  SSID/strength and current power — that the SensorAgent persists as it polls (read from the DB; the
  device itself accepts only one client connection, held by the agent), plus statistics over its
  collected readings.
- Delete a device from the **Devices** page: the per-row **Delete** button opens a confirmation modal
  warning that all collected data is permanently deleted, then removes the device and cascade-deletes
  its `EnergyReading` rows.
- Rename a device from the **Devices** page: a new `Devices/Edit` page (wired to the existing per-row
  **Edit** button) lets you change a device's friendly name. The name is required and length-validated,
  persisted directly, and never overwritten by the polling `Worker` or HomeWizard discovery.
- `CLAUDE.md` with build/run commands, EF migration workflow, and architecture notes for AI agents.
- Production deployment tooling under `deploy/`: a `systemd` one-shot migration unit
  (`HomeMonitoringMigration`), ordering drop-ins that make the agent and dashboard wait for it,
  a `deploy.sh` helper, and `deploy/README.md` documenting the self-contained `linux-arm64`
  host layout, deploy, and rollback procedure.
- Required, data-annotation-validated `Email` settings (`EmailSettings`) with fail-fast
  `ValidateOnStart`; `SmtpHost`/`SmtpPort` are supplied from the Aspire `mailpit` connection string.

### Changed

Author: *Claude Opus 5.5*

- **Production logs to the laptop's Seq again, from the secrets.** The first deploy of the change below restored the
  GitHub Secrets `ConnectionStrings__seq` and `SeqApiKey`, which still held piserver's (decommissioned) Seq; the
  deployed files had been pointed at the laptop's Seq by hand. Bert set both secrets to the laptop's values (one key
  for both apps) and redeployed. `deploy/README.md` gains "Changing a production value", and `AGENTS.md` the rule:
  change the secret and deploy, never only the file on the box.
- **Over 95 % less log volume in Seq** (measured: 7,192 → 81 HomeMonitoring events in 10 minutes, no duplicates, no
  errors). Both apps set `System.Net.Http.HttpClient` to `Warning`, and the Web
  app got the `Serilog` section it never had (`Microsoft` at `Warning`), so EF Core's "Executed DbCommand" and the
  four Information lines per HTTP request are gone; that also stops the Hue bridge URL, which contains the Hue API
  key, from being logged. Serilog's OpenTelemetry log sink is removed from both apps: the laptop's collector
  forwarded those logs to Seq too, so every event was stored twice. Traces and metrics still go over OTLP. Before:
  about a million events a day, ~100,000 of them "HTTP request failed" for three HomeWizard sockets retired on
  2026-10-18.

Author: *Bert Berrevoets, Claude Opus 5.5*

- AI agent setup (2026-11): the instructions moved from `CLAUDE.md` to `AGENTS.md` (single source for Claude Code,
  Codex and other agents) with a `CLAUDE.md` stub that imports it; repo-init blocks in `.gitignore` and
  `.gitattributes`; Conventions section in `README.md` (now saved as UTF-8 without BOM). An untracked
  `AGENTS.md` that Codex had generated as a copy of `CLAUDE.md` was dropped. `aspire.config.json` (the Aspire CLI
  project file that points `aspire run` at `AppHost/AppHost.csproj`) is now tracked.

Author: *Bert Berrevoets*

- The SensorAgent now polls all HomeWizard devices **concurrently** (one DI scope + `DbContext` per
  device) instead of sequentially, so a slow or unreachable device no longer delays the others.
- The SensorAgent now persists each device's last-known status (WiFi every poll; firmware/API a few
  times a day) on the `Device` row, so the dashboard and Details pages can show it without contacting
  the connection-limited device.
- Raised the SensorAgent `Email:DeviceOfflineThresholdMinutes` from 1 to 5.
- Renamed Serilog enrichment property `Application` to `Service` across Web and SensorAgent.
- Upgraded to .NET 10 GA, Aspire 13, and EF Core 10.
- Bumped NuGet packages across all projects.
- Raised the markdownlint line-length limit to 180 to match the documented standard.

### Fixed

Author: *Claude Opus 5.5*

- **Deleting a device works again for devices with history.** The cascade delete put all of a device's
  `EnergyReading` rows into one statement, which ran past SQL Server's 30 s command timeout on the remote database
  ("Execution Timeout Expired"), so the device stayed and kept being polled. Delete now disables the device first,
  removes the readings in batches of 5000 and then the device, and reports how many readings went.

Author: *Bert Berrevoets*

- The device **Details** page showed "unreachable" even while the device was being polled
  successfully. HomeWizard sockets accept only ~one client connection, which the SensorAgent holds, so
  the web page could never reach the device directly. The Details page now reads the device's last-known
  status (firmware / WiFi / power) from the DB — persisted by the agent as it polls — and never contacts
  the device (adds a `Device` status-columns migration).
- Devices that were online were intermittently reported **offline**. Fixed by concurrent polling
  (above), a dedicated resilience-free `HttpClient` for device calls (no retries/circuit-breaker for
  expected-offline LAN devices), and a startup-grace window in `DeviceMonitoringService` so a restart's
  stale `LastSeenAt` can't fire a false alert before the `Worker` re-polls.
- The **Devices** list and **Details** pages are served `no-store`, so their volatile Last Seen and
  reading-count values are never shown from the browser's HTTP or back-forward cache.
- Removed resilience-handler log spam on LAN polling: HomeWizard device polls no longer retry (and log
  a Warning) for every expected-offline device, and Philips Hue polling no longer emits a per-attempt
  Polly telemetry line every 2s.
- Dark mode no longer flashes the light theme on every page navigation (FOUC). The stored theme is
  now applied by an inline, render-blocking script in the layout `<head>` before first paint,
  instead of by `theme-toggle.js` at the end of `<body>` on `DOMContentLoaded`.
- `PhilipsHueLightMonitorService` now handles cancellation cleanly instead of logging on shutdown.
- Dropped `HealthChecks.UI` to unblock EF Core 10 startup.
- OpenTelemetry `service.name` is now set explicitly on the resource (and on the Serilog OTLP
  sink), so traces and logs are no longer reported as `unknown_service` when a service runs
  outside Aspire (e.g. exporting straight to an external OTLP/Grafana stack).

### Removed

Author: *Bert Berrevoets*

- Per-project `appsettings.Development.json` files (now provided via Aspire / user secrets).

### Security

Author: *Bert Berrevoets, Claude Opus 5.5*

- `AspNetCore.HealthChecks.Network` removed from the SensorAgent: none of its health checks is used, and it pulled
  `SSH.NET` 2023.0.0 (CVE-2026-48798, high; its newest 9.0.0 still does).
- One Aspire version again: `Aspire.AppHost.Sdk` 9.4.2 → 13.6.4 and all Aspire packages 13.4.6 → 13.6.4
  (`MessagePack` 2.5.302 → 2.5.307), `CommunityToolkit.Aspire.Hosting.MailPit` 13.4.0 → 13.6.1; EF Core and
  `Microsoft.Extensions.Hosting` 10.0.10 → 10.0.14 (required by the Aspire 13.6 EF integration).
- `System.Security.Cryptography.Pkcs` 10.0.14 referenced directly in Shared and the AppHost (CVE-2026-84008):
  `Microsoft.Data.SqlClient` still brings 10.0.12.
- Aspire 13.6 runs Seq and Mailpit without a proxy, and their health checks on `localhost` timed out on `::1`,
  so the SensorAgent and the web app stayed in Waiting: the Seq `http` and Mailpit `http` / `smtp` endpoints are
  proxied again. Verified with an AppHost run on 2026-11-03 (fresh containers): all resources healthy after 43 s,
  migration exit 0, `/`, `/Devices`, `/health` and `/alive` → 200, no warnings in the SensorAgent log.

## [2025-09-13]

### Added

Author: *Bert Berrevoets*

- Philips Hue integration: bridge pairing, light discovery, on/off + brightness control, persistence
  of light state, and real-time SignalR updates pushed from a hosted monitor that diffs against
  in-memory previous state to suppress self-triggered toasts.
- Toaster-based notification system replacing inline alert divs.

### Changed

Author: *Bert Berrevoets*

- README clarified to direct users to the Aspire Dashboard for dynamically-assigned ports.

## [2025-09-12]

### Added

Author: *Bert Berrevoets*

- Dark mode with persistent theme toggle.
- Additional health checks and custom OpenTelemetry meters (`HomeMonitoring.SensorAgent`,
  `HomeMonitoring.Web`).

### Changed

Author: *Bert Berrevoets*

- Upgraded solution to .NET 10.0.
- Bumped NuGet packages across all projects.

Author: *dependabot[bot]*

- Bumped NuGet group dependencies (PRs #6, #7).

## [2025-09-01]

### Added

Author: *Bert Berrevoets*

- `HomeMonitoring.MigrationService` — Aspire-orchestrated EF Core migration runner that completes
  before downstream services start.
- Enhanced dashboard features and Bootstrap styling.
- Copyright and licensing headers on source files.

### Changed

Author: *Bert Berrevoets*

- Reordered project components and updated dependencies.
- Improved device monitoring with better error handling and email notifications.

## [2025-08-29]

### Added

Author: *Bert Berrevoets*

- Initial repository scaffolding: `.gitattributes`, `.gitignore`, `README.md`.
- `HomeMonitoring.SensorAgent` background service with database initialization, HomeWizard device
  polling, device-status monitoring, and email alerts via Mailpit.
- `HomeMonitoring.Web` Razor Pages dashboard with SignalR-pushed live energy data.
- HomeWizard integration supporting `HWE-P1` smart meters and `HWE-SKT` energy sockets.
- Aspire `AppHost` orchestrating SQL Server, Seq, Mailpit, SensorAgent, and Web.
