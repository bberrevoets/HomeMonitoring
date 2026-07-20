# Production Deployment

This directory documents how HomeMonitoring runs in production and how to deploy updates.
It does **not** run under Aspire in production — the three .NET services run directly as
`systemd` units on a Linux host.

## Topology

The production host is an **ARM64 (aarch64) Linux** machine with **no shared .NET runtime**,
so every service is published **self-contained** for `linux-arm64`. Each service lives in its
own directory under the `bert` user's home and is managed by a `systemd` unit:

| Service | Directory | systemd unit | Type |
| --- | --- | --- | --- |
| Migration runner | `~/MigrationLinux` | `HomeMonitoringMigration.service` | `oneshot` (runs, applies migrations, exits) |
| Sensor agent | `~/AgentLinux` | `HomeMonitoringSensorAgent.service` | `simple` (`Restart=always`) |
| Web dashboard | `~/WebLinux` | `HomeMonitoringDashboard.service` | `simple` (`Restart=always`), behind nginx on `0.0.0.0:5000` |

The database, Seq, and Mailpit are external services referenced from each app's
`appsettings.Production.json` (which holds host-specific config and secrets and is **never**
overwritten by a deploy).

## Migration ordering

In Aspire the migration runner completes before the apps start (`WaitForCompletion`). The same
ordering is reproduced on the server with `systemd`:

- `HomeMonitoringMigration.service` declares `Before=` both app units.
- Each app unit gets an `After=` + `Wants=` drop-in
  (`<unit>.service.d/10-wait-migration.conf`), added via `systemctl edit`, so the base unit
  files stay untouched.

On boot, `systemd` runs the migration one-shot first, then starts the agent and dashboard.
Because the one-shot uses `RemainAfterExit=yes`, on a **live** host you must **restart** it
(not `start`) to re-apply migrations — `deploy.sh` does this for you.

## Files here

- [`deploy.sh`](deploy.sh) — server-side deploy helper (see below).
- [`systemd/HomeMonitoringMigration.service`](systemd/HomeMonitoringMigration.service) — the one-shot migration unit.
- `systemd/<app>.service.d/10-wait-migration.conf` — the ordering drop-ins.
- `systemd/HomeMonitoring{SensorAgent,Dashboard}.service` — reference copies of the base app units.

## First-time setup on a new host

```bash
# 1. Install .NET nothing needed — services are self-contained.
# 2. Create the three app directories and drop the published output + appsettings.Production.json in each.
# 3. Install the units (run from a checkout of this repo):
sudo cp deploy/systemd/HomeMonitoringMigration.service /etc/systemd/system/
sudo cp deploy/systemd/HomeMonitoringSensorAgent.service  /etc/systemd/system/   # if not already present
sudo cp deploy/systemd/HomeMonitoringDashboard.service    /etc/systemd/system/   # if not already present
sudo mkdir -p /etc/systemd/system/HomeMonitoringSensorAgent.service.d \
              /etc/systemd/system/HomeMonitoringDashboard.service.d
sudo cp deploy/systemd/HomeMonitoringSensorAgent.service.d/10-wait-migration.conf \
        /etc/systemd/system/HomeMonitoringSensorAgent.service.d/
sudo cp deploy/systemd/HomeMonitoringDashboard.service.d/10-wait-migration.conf \
        /etc/systemd/system/HomeMonitoringDashboard.service.d/
sudo systemctl daemon-reload
sudo systemctl enable HomeMonitoringMigration.service \
                      HomeMonitoringSensorAgent.service HomeMonitoringDashboard.service
# 4. Copy deploy.sh to the host home dir:
scp deploy/deploy.sh server:~/ && ssh server 'chmod +x ~/deploy.sh'
```

## Deploying an update

From the **dev machine**, publish each changed service self-contained for `linux-arm64`, pack it
(excluding `appsettings*.json`), and upload it to the staging dir:

```bash
mkdir -p out
for proj in HomeMonitoring.SensorAgent HomeMonitoring.Web HomeMonitoring.MigrationService; do
  dotnet publish "$proj/$proj.csproj" -c Release -r linux-arm64 --self-contained true \
    -p:ErrorOnDuplicatePublishOutputFiles=false -o "out/$proj"
done
tar czf agent.tar.gz     --exclude='appsettings*.json' -C out/HomeMonitoring.SensorAgent .
tar czf web.tar.gz       --exclude='appsettings*.json' -C out/HomeMonitoring.Web .
tar czf migration.tar.gz --exclude='appsettings*.json' -C out/HomeMonitoring.MigrationService .

ssh server 'mkdir -p /tmp/hm-deploy'
scp agent.tar.gz web.tar.gz migration.tar.gz server:/tmp/hm-deploy/
```

Then on the **server**, run the helper — it deploys whichever tarballs are present, applies
migrations, and restarts the services in the right order:

```bash
ssh server './deploy.sh'
```

`deploy.sh` stops the apps, syncs each staged tarball into place (keeping a `<dir>.bak` rollback
snapshot and preserving `appsettings*.json`), restarts the migration one-shot, then starts the
apps again.

> When a release adds an EF migration, **always** include `migration.tar.gz` — the migration is
> compiled into `HomeMonitoring.MigrationService`, so a pending migration only applies if
> `~/MigrationLinux` is updated too.

## Rollback

Each deploy leaves a `~/<App>Linux.bak` snapshot of the previous release:

```bash
ssh server 'sudo systemctl stop HomeMonitoringSensorAgent HomeMonitoringDashboard \
  && rsync -a --delete --exclude "appsettings*.json" ~/AgentLinux.bak/ ~/AgentLinux/ \
  && sudo systemctl start HomeMonitoringSensorAgent HomeMonitoringDashboard'
```

(EF migrations are not automatically rolled back — restore the database separately if a schema
change must be reverted.)
