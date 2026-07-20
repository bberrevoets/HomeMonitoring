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

The database, Seq, and Mailpit are external services. Each app ships a single
`appsettings.json` in which every secret is the literal marker `"InSecrets"`; the deploy
pipeline replaces those markers with the real host-specific values (see
[Secrets and the `InSecrets` convention](#secrets-and-the-insecrets-convention)) before the
tokenized file lands on the server.

## Migration ordering

In Aspire the migration runner completes before the apps start (`WaitForCompletion`). The same
ordering is reproduced on the server with `systemd`:

- `HomeMonitoringMigration.service` declares `Before=` both app units.
- Each app unit gets an `After=` + `Requires=` drop-in
  (`<unit>.service.d/10-wait-migration.conf`), added via `systemctl edit`, so the base unit
  files stay untouched.

On boot, `systemd` runs the migration one-shot first, then starts the agent and dashboard. The
dependency is `Requires=` (not `Wants=`), so **if the migration fails the apps are not started** —
they never run against an unmigrated/incompatible schema (a failed boot leaves the stack down
until the migration can complete, which is the safe outcome). Because the one-shot uses
`RemainAfterExit=yes`, on a **live** host you must **restart** it (not `start`) to re-apply
migrations — `deploy.sh` does this for you, and aborts without starting the apps if it fails.

## Files here

- [`deploy.sh`](deploy.sh) — server-side deploy helper (see below).
- [`systemd/HomeMonitoringMigration.service`](systemd/HomeMonitoringMigration.service) — the one-shot migration unit.
- `systemd/<app>.service.d/10-wait-migration.conf` — the ordering drop-ins.
- `systemd/HomeMonitoringDashboard.service.d/20-environment.conf` — host settings for the dashboard
  (Kestrel listen address; optional OTLP endpoint).
- `systemd/HomeMonitoring{SensorAgent,Dashboard}.service` — reference copies of the base app units.

## First-time setup on a new host

```bash
# 1. Install .NET nothing needed — services are self-contained.
# 2. Create the three app directories (~/MigrationLinux ~/AgentLinux ~/WebLinux). The deploy
#    pipeline fills them, including a tokenized appsettings.json — no manual appsettings to drop in.
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
# Host settings the deploy must not manage (see "Host-specific settings" below) — most importantly
# the dashboard's HTTP listen address, so it stays reachable after a deploy replaces appsettings.json:
sudo cp deploy/systemd/HomeMonitoringDashboard.service.d/20-environment.conf \
        /etc/systemd/system/HomeMonitoringDashboard.service.d/
sudo systemctl daemon-reload
sudo systemctl enable HomeMonitoringMigration.service \
                      HomeMonitoringSensorAgent.service HomeMonitoringDashboard.service
# 4. Copy deploy.sh to the host home dir:
scp deploy/deploy.sh server:~/ && ssh server 'chmod +x ~/deploy.sh'
```

## Host-specific settings (listen address, OTLP)

The deploy ships and overwrites each app's `appsettings.json`, so anything that must differ per host
and must **not** be wiped by a deploy lives in the systemd unit instead — via the
`HomeMonitoringDashboard.service.d/20-environment.conf` drop-in:

- **`ASPNETCORE_URLS=http://0.0.0.0:5000`** — the dashboard's HTTP listen address. Without it Kestrel
  binds `localhost` only and the site is unreachable from the LAN. Kept in the unit so a deploy can't
  remove it, and because it's ignored under Aspire (dev is unaffected).
- **`OTEL_EXPORTER_OTLP_ENDPOINT`** (optional, commented by default) — the OTLP collector for traces
  and metrics. The OpenTelemetry exporter reads it from the **environment**, not `appsettings.json`,
  so it belongs here too. Uncomment and set your collector host to enable export (add the same line to
  a SensorAgent drop-in to restore its telemetry).

Apply changes with `sudo systemctl daemon-reload && sudo systemctl restart HomeMonitoringDashboard`.

> Everything else an app needs in production must live in the committed `appsettings.json` (non-secret)
> or the `InSecrets` set (secret): a deploy replaces the on-server `appsettings.json`, so any config
> that exists only on the box is lost on the next deploy.

## Secrets and the `InSecrets` convention

Every app has a single committed `appsettings.json`. Secret values are the literal marker
`"InSecrets"`; real values are supplied per environment:

- **Development** — .NET User Secrets (`dotnet user-secrets`), loaded only in the Development
  environment, override the markers. Aspire additionally injects the `ConnectionStrings__*`
  values as environment variables.
- **Production** — the deploy replaces each `"InSecrets"` marker with a real value pulled from
  **GitHub Actions Secrets**, then ships the resulting `appsettings.json`.

The secret keys and their matching GitHub Secret names (config path joined with `__`, since a
secret name can't contain `:`):

| Config key | GitHub Secret | Migration | SensorAgent | Web |
| --- | --- | --- | --- | --- |
| `ConnectionStrings:sensorsdb` | `ConnectionStrings__sensorsdb` | yes | yes | yes |
| `ConnectionStrings:seq` | `ConnectionStrings__seq` | no | yes | yes |
| `ConnectionStrings:mailpit` | `ConnectionStrings__mailpit` | no | yes | no |
| `SeqApiKey` | `SeqApiKey` | no | yes | yes |
| `Email:SmtpUsername` | `Email__SmtpUsername` | no | yes | no |
| `Email:SmtpPassword` | `Email__SmtpPassword` | no | yes | no |

[`.github/scripts/replace-secrets.py`](../.github/scripts/replace-secrets.py) performs the
substitution: it walks each published `appsettings.json`, and for every leaf equal to
`"InSecrets"` looks up the env var named by the `__`-joined key path. **It fails the build if
any marker has no matching secret**, so an `InSecrets` literal can never reach production.

## GitHub Actions deploy (self-hosted runner)

Production is deployed by the **Deploy to on-prem** workflow
([`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml)), triggered manually
(`workflow_dispatch`). Because the target box is on a private LAN, the job runs on a
**self-hosted runner installed on the box itself** — it builds, injects the secrets, packages
the tarballs, and runs the checked-out `deploy/deploy.sh` locally (no scp/ssh, no inbound port).

One-time setup:

1. Install a self-hosted runner as user `bert` with labels `linux, ARM64`
   (GitHub → Settings → Actions → Runners). Ensure `python3` and the .NET SDK are on its `PATH`.
2. Grant the runner user **NOPASSWD sudo** limited to the `systemctl` verbs `deploy.sh` uses
   (`stop` / `start` / `restart` of the three units).
3. Provide the six **Secrets** listed above. The deploy job runs in the `production` **Environment**
   (declared in the workflow), so you can add them either as repository secrets
   (Settings → Secrets and variables → Actions) or as environment secrets on the `production`
   environment — the job resolves both. Add a **required reviewer** to that environment to gate each
   deploy behind a manual approval (recommended).

Trigger a deploy from the **Actions** tab (or `gh workflow run "Deploy to on-prem"`).

## Deploying an update (manual fallback)

The normal path is the GitHub Actions workflow above. To deploy by hand, publish each changed
service self-contained for `linux-arm64`, **inject the secrets yourself** (replace the
`InSecrets` markers in each `out/<proj>/appsettings.json` — e.g. run `replace-secrets.py` with
the `ConnectionStrings__*` / `SeqApiKey` / `Email__*` values exported as env vars), pack it (now
**including** `appsettings.json`), and upload it to the staging dir:

```bash
mkdir -p out
for proj in HomeMonitoring.SensorAgent HomeMonitoring.Web HomeMonitoring.MigrationService; do
  dotnet publish "$proj/$proj.csproj" -c Release -r linux-arm64 --self-contained true \
    -p:ErrorOnDuplicatePublishOutputFiles=false -o "out/$proj"
done
# ... inject secrets into out/*/appsettings.json here (replace-secrets.py) ...
tar czf agent.tar.gz     -C out/HomeMonitoring.SensorAgent .
tar czf web.tar.gz       -C out/HomeMonitoring.Web .
tar czf migration.tar.gz -C out/HomeMonitoring.MigrationService .

ssh server 'mkdir -p /tmp/hm-deploy'
scp agent.tar.gz web.tar.gz migration.tar.gz server:/tmp/hm-deploy/
```

Then on the **server**, run the helper — it deploys whichever tarballs are present, applies
migrations, and restarts the services in the right order:

```bash
ssh server './deploy.sh'
```

`deploy.sh` stops the apps, syncs each staged tarball into place (keeping a `<dir>.bak` rollback
snapshot, and `chmod 600`-ing the deployed `appsettings.json` since it now carries the real
secrets), restarts the migration one-shot, starts the apps again, and removes the staged tarballs.

> When a release adds an EF migration, **always** include `migration.tar.gz` — the migration is
> compiled into `HomeMonitoring.MigrationService`, so a pending migration only applies if
> `~/MigrationLinux` is updated too.

## Rollback

Each deploy leaves a `~/<App>Linux.bak` snapshot of the previous release:

```bash
ssh server 'sudo systemctl stop HomeMonitoringSensorAgent HomeMonitoringDashboard \
  && rsync -a --delete ~/AgentLinux.bak/ ~/AgentLinux/ \
  && sudo systemctl start HomeMonitoringSensorAgent HomeMonitoringDashboard'
```

(EF migrations are not automatically rolled back — restore the database separately if a schema
change must be reverted.)
