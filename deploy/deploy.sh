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
# Normal path: the "Deploy to on-prem" GitHub Actions workflow runs on a self-hosted
# runner on this box — it publishes, replaces the "InSecrets" markers in each
# appsettings.json with real values from GitHub Secrets, stages the tarballs, and calls
# this script. To deploy by hand instead, publish + inject secrets yourself, then:
#   proj=HomeMonitoring.SensorAgent          # or .Web / .MigrationService
#   dotnet publish $proj/$proj.csproj -c Release -r linux-arm64 --self-contained true \
#       -p:ErrorOnDuplicatePublishOutputFiles=false -o out
#   tar czf agent.tar.gz -C out .            # appsettings.json IS shipped (already tokenized)
#   scp agent.tar.gz server:/tmp/hm-deploy/
#   ssh server './deploy.sh'
#
# IMPORTANT: when a release adds an EF migration, always stage migration.tar.gz
# too — the pending migration only applies if ~/MigrationLinux is updated.
# Each target's appsettings.json is overwritten by the deployed (tokenized) copy and
# chmod'd 600, since it now carries the real secrets.
#
set -uo pipefail

STAGING="${1:-/tmp/hm-deploy}"
AGENT_SVC=HomeMonitoringSensorAgent
WEB_SVC=HomeMonitoringDashboard
MIG_SVC=HomeMonitoringMigration

# Reconcile helper ensures the systemd drop-ins exist (installed once; see deploy/README.md).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RECONCILE=/usr/local/sbin/hm-reconcile-units
DASHBOARD_LISTEN='0.0.0.0:5000'   # expected Kestrel bind from the ASPNETCORE_URLS drop-in

say()  { printf '\n\033[1;36m== %s ==\033[0m\n' "$*"; }
die()  { printf '\033[1;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

have() { [[ -f "$STAGING/$1" ]]; }

sync_app() { # <tarball> <target-dir> <host-exe>
  local tarball="$STAGING/$1" dir="$2" exe="$3" tmp
  tmp="$(mktemp -d)" || die "mktemp failed"
  tar xzf "$tarball" -C "$tmp" || die "extract $tarball failed"
  # Refresh the rollback snapshot. cp -a preserves the source's (often read-only,
  # dr-x------) directory mode, so make the snapshot writable — otherwise neither the
  # next deploy's cleanup nor a manual `rm -rf` can unlink files inside it.
  [[ -e "$dir.bak" ]] && chmod -R u+w "$dir.bak"
  rm -rf "$dir.bak"
  cp -a "$dir" "$dir.bak" || die "backup of $dir failed"
  chmod -R u+w "$dir.bak"
  rsync -a --delete "$tmp/" "$dir/" || die "rsync into $dir failed"
  chmod 600 "$dir/appsettings.json" 2>/dev/null || true
  chmod +x "$dir/$exe" 2>/dev/null || true
  chmod +x "$dir/createdump" 2>/dev/null || true
  rm -rf "$tmp"
  echo "  deployed $1 -> $dir  (rollback: $dir.bak)"
}

have migration.tar.gz || have agent.tar.gz || have web.tar.gz \
  || die "no tarballs in $STAGING (expected migration.tar.gz / agent.tar.gz / web.tar.gz)"

# Ensure the host-setting drop-ins exist (the dashboard's ASPNETCORE_URLS bind), self-healing a
# wiped/missing one. hm-reconcile-units synthesizes the content itself, creates it only when absent
# (it never overwrites a present drop-in, so a manual change is preserved), and ignores its argument
# (passed only so the `*` sudoers pattern keeps matching). Aborts the deploy if the helper fails.
# A present-but-wrong drop-in is caught later by the runtime bind check. Requires the one-time helper
# + sudoers install (deploy/README.md); skipped when the helper is not installed.
if [[ -x "$RECONCILE" ]]; then
  say "Ensuring systemd drop-ins"
  sudo "$RECONCILE" "$SCRIPT_DIR/systemd" || die "systemd drop-in reconcile failed"
else
  echo "  hm-reconcile-units not installed — skipping systemd drop-in check"
fi

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

# The staged tarballs contain the tokenized appsettings.json (real secrets) — remove them.
say "Cleaning staging"
rm -f "$STAGING"/*.tar.gz

# Fail loudly if the dashboard did not come up on the expected LAN-reachable bind. Catches a missing
# or wrong ASPNETCORE_URLS drop-in (Kestrel falling back to localhost) instead of reporting a green
# deploy while the site is unreachable from the LAN.
say "Verifying dashboard bind ($DASHBOARD_LISTEN)"
bound=0
for _ in $(seq 1 15); do
  if ss -tln 2>/dev/null | awk '{print $4}' | grep -qFx "$DASHBOARD_LISTEN"; then bound=1; break; fi
  sleep 1
done
[[ "$bound" == 1 ]] || die "$WEB_SVC is not listening on $DASHBOARD_LISTEN after start —
  the ASPNETCORE_URLS bind is missing or wrong (Kestrel may be bound to localhost only).
  Inspect: ss -tlnp | grep :5000  and  ${WEB_SVC}.service.d/20-environment.conf"
echo "  dashboard is listening on $DASHBOARD_LISTEN"

say "Status"
for svc in "$MIG_SVC" "$AGENT_SVC" "$WEB_SVC"; do
  printf '  %-32s %s\n' "$svc" "$(systemctl is-active "$svc" || true)"
done
echo
echo "Done. Tail logs with:  journalctl -u $AGENT_SVC -u $WEB_SVC -f"
