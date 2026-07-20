# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Attributors

- Bert Berrevoets (B.N. Berrevoets)
- dependabot[bot]

## [Unreleased]

### Added

Author: *Bert Berrevoets*

- `CLAUDE.md` with build/run commands, EF migration workflow, and architecture notes for AI agents.
- Production deployment tooling under `deploy/`: a `systemd` one-shot migration unit
  (`HomeMonitoringMigration`), ordering drop-ins that make the agent and dashboard wait for it,
  a `deploy.sh` helper, and `deploy/README.md` documenting the self-contained `linux-arm64`
  host layout, deploy, and rollback procedure.
- Required, data-annotation-validated `Email` settings (`EmailSettings`) with fail-fast
  `ValidateOnStart`; `SmtpHost`/`SmtpPort` are supplied from the Aspire `mailpit` connection string.

### Changed

Author: *Bert Berrevoets*

- Renamed Serilog enrichment property `Application` to `Service` across Web and SensorAgent.
- Upgraded to .NET 10 GA, Aspire 13, and EF Core 10.
- Bumped NuGet packages across all projects.
- Raised the markdownlint line-length limit to 180 to match the documented standard.

### Fixed

Author: *Bert Berrevoets*

- `PhilipsHueLightMonitorService` now handles cancellation cleanly instead of logging on shutdown.
- Dropped `HealthChecks.UI` to unblock EF Core 10 startup.
- OpenTelemetry `service.name` is now set explicitly on the resource (and on the Serilog OTLP
  sink), so traces and logs are no longer reported as `unknown_service` when a service runs
  outside Aspire (e.g. exporting straight to an external OTLP/Grafana stack).

### Removed

Author: *Bert Berrevoets*

- Per-project `appsettings.Development.json` files (now provided via Aspire / user secrets).

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
