#!/usr/bin/env bash
#
# Reconcile the systemd drop-ins that carry host settings a deploy must keep in sync — most
# importantly the dashboard's ASPNETCORE_URLS bind. `deploy.sh` calls this every deploy so a
# drop-in change ships like code and a wiped drop-in self-heals, instead of relying on the
# one-time first-time-setup step (which a plain redeploy would otherwise miss).
#
# It runs as root via a single, exact NOPASSWD sudoers entry:
#     bert ALL=(root) NOPASSWD: /usr/local/sbin/hm-reconcile-units *
# Install once with:  sudo install -m755 deploy/reconcile-units.sh /usr/local/sbin/hm-reconcile-units
#
# The managed set is a hardcoded list (never a wildcard), so this can only ever write the
# specific drop-ins below — it can't be coaxed into installing an arbitrary systemd unit.
#
# Usage: hm-reconcile-units <systemd-source-dir>   # the repo's deploy/systemd directory
set -uo pipefail

SRC="${1:?usage: hm-reconcile-units <systemd-source-dir>}"
DEST=/etc/systemd/system

MANAGED=(
  "HomeMonitoringDashboard.service.d/20-environment.conf"
)

changed=0
for rel in "${MANAGED[@]}"; do
  src="$SRC/$rel" dst="$DEST/$rel"
  [[ -f "$src" ]] || { echo "  reconcile: source $rel missing, skipping"; continue; }
  if ! cmp -s "$src" "$dst" 2>/dev/null; then
    install -D -m644 "$src" "$dst" || { echo "reconcile: install $dst failed" >&2; exit 1; }
    echo "  reconciled $dst"
    changed=1
  fi
done

if [[ "$changed" == 1 ]]; then
  systemctl daemon-reload
  echo "  systemd daemon reloaded"
else
  echo "  systemd drop-ins already up to date"
fi
