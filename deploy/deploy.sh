#!/usr/bin/env bash
#
# HomeMonitoring production deploy helper (run ON the server).
#
# Deploys whichever of these self-contained linux-arm64 tarballs are staged in
# the staging dir (default /tmp/hm-deploy), preserving each target's config:
#
#   migration.tar.gz -> ~/MigrationLinux  (HomeMonitoringMigration.service, one-shot)
#   agent.tar.gz     -> ~/AgentLinux      (HomeMonitoringSensorAgent.service)
#   web.tar.gz       -> ~/WebLinux        (HomeMonitoringDashboard.service)
#
# Typical use, from the DEV machine:
#   proj=HomeMonitoring.SensorAgent          # or .Web / .MigrationService
#   dotnet publish $proj/$proj.csproj -c Release -r linux-arm64 --self-contained true \
#       -p:ErrorOnDuplicatePublishOutputFiles=false -o out
#   tar czf agent.tar.gz --exclude='appsettings*.json' -C out .
#   scp agent.tar.gz server:/tmp/hm-deploy/
#   ssh server './deploy.sh'
#
# IMPORTANT: when a release adds an EF migration, always stage migration.tar.gz
# too — the pending migration only applies if ~/MigrationLinux is updated.
# appsettings*.json in each target dir is never overwritten (secrets/host config).
#
set -uo pipefail

STAGING="${1:-/tmp/hm-deploy}"
AGENT_SVC=HomeMonitoringSensorAgent
WEB_SVC=HomeMonitoringDashboard
MIG_SVC=HomeMonitoringMigration

say()  { printf '\n\033[1;36m== %s ==\033[0m\n' "$*"; }
die()  { printf '\033[1;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

have() { [[ -f "$STAGING/$1" ]]; }

sync_app() { # <tarball> <target-dir> <host-exe>
  local tarball="$STAGING/$1" dir="$2" exe="$3" tmp
  tmp="$(mktemp -d)" || die "mktemp failed"
  tar xzf "$tarball" -C "$tmp" || die "extract $tarball failed"
  rm -rf "$dir.bak"; cp -a "$dir" "$dir.bak" || die "backup of $dir failed"
  rsync -a --delete --exclude 'appsettings*.json' "$tmp/" "$dir/" || die "rsync into $dir failed"
  chmod +x "$dir/$exe" 2>/dev/null || true
  chmod +x "$dir/createdump" 2>/dev/null || true
  rm -rf "$tmp"
  echo "  deployed $1 -> $dir  (rollback: $dir.bak)"
}

have migration.tar.gz || have agent.tar.gz || have web.tar.gz \
  || die "no tarballs in $STAGING (expected migration.tar.gz / agent.tar.gz / web.tar.gz)"

say "Stopping application services"
sudo systemctl stop "$AGENT_SVC" "$WEB_SVC"

have migration.tar.gz && { say "Updating MigrationService"; sync_app migration.tar.gz "$HOME/MigrationLinux" HomeMonitoring.MigrationService; }
have agent.tar.gz     && { say "Updating SensorAgent";     sync_app agent.tar.gz     "$HOME/AgentLinux"     HomeMonitoring.SensorAgent; }
have web.tar.gz       && { say "Updating Web dashboard";   sync_app web.tar.gz       "$HOME/WebLinux"       HomeMonitoring.Web; }

# Always (re)run migrations before the apps come up. RemainAfterExit means the
# one-shot must be *restarted* to re-run on a live host (a reboot re-runs it).
# If migrations fail, abort WITHOUT starting the apps — they must never run against an
# unmigrated schema. The apps are already stopped; the previous release is in <dir>.bak.
say "Applying database migrations"
sudo systemctl restart "$MIG_SVC"
if [[ "$(systemctl is-active "$MIG_SVC" || true)" != "active" ]]; then
  die "$MIG_SVC failed — apps left stopped to avoid running against an unmigrated schema.
  Inspect: journalctl -u $MIG_SVC -n 50
  Roll back a synced app from its ~/<App>Linux.bak snapshot if needed."
fi

say "Starting application services"
sudo systemctl start "$AGENT_SVC" "$WEB_SVC"

say "Status"
for svc in "$MIG_SVC" "$AGENT_SVC" "$WEB_SVC"; do
  printf '  %-32s %s\n' "$svc" "$(systemctl is-active "$svc" || true)"
done
echo
echo "Done. Tail logs with:  journalctl -u $AGENT_SVC -u $WEB_SVC -f"
