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

### Changed

Author: *Bert Berrevoets*

- Renamed Serilog enrichment property `Application` to `Service` across Web and SensorAgent.
- Upgraded to .NET 10 GA, Aspire 13, and EF Core 10.

### Fixed

Author: *Bert Berrevoets*

- `PhilipsHueLightMonitorService` now handles cancellation cleanly instead of logging on shutdown.
- Dropped `HealthChecks.UI` to unblock EF Core 10 startup.

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
