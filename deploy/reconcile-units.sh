#!/usr/bin/env bash
#
# Reconcile the systemd drop-ins that carry host settings a deploy must keep in sync — currently
# the dashboard's ASPNETCORE_URLS bind. deploy.sh runs this each deploy so the bind ships like code
# and a wiped drop-in self-heals, instead of relying only on the one-time first-time-setup step.
#
# SECURITY: the content is SYNTHESIZED below, never copied from a caller-supplied path. This helper
# runs as root via NOPASSWD sudo; copying an arbitrary source would let a compromised runner install
# malicious unit directives (e.g. User=root / ExecStart=...) or symlink root-only files into a
# world-readable drop-in. Command-line arguments are therefore IGNORED (one is still accepted so the
# `hm-reconcile-units *` sudoers pattern keeps matching).
#
# The DASHBOARD_ENV heredoc below must stay byte-identical to
# deploy/systemd/HomeMonitoringDashboard.service.d/20-environment.conf (the reference / first-cp copy).
#
# Install once:  sudo install -m755 deploy/reconcile-units.sh /usr/local/sbin/hm-reconcile-units
set -uo pipefail

DEST=/etc/systemd/system
CHANGED=0

DASHBOARD_ENV=$(cat <<'EOF'
[Service]
# HTTP listen address for the dashboard. This is a host/deployment concern, so it lives in the
# unit (as a drop-in) rather than appsettings.json: a deploy of the base appsettings.json can
# never wipe it, and it never affects Aspire dev (systemd isn't used there). 0.0.0.0 = listen on
# all interfaces (LAN-reachable); use 127.0.0.1 to expose the app only through a local proxy.
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
EOF
)

reconcile() { # <relative-path-under-DEST> <desired-content>
  local dst="$DEST/$1" desired="$2"
  if [[ -f "$dst" && "$(cat -- "$dst" 2>/dev/null)" == "$desired" ]]; then
    return 0
  fi
  mkdir -p -- "$(dirname -- "$dst")" || { echo "reconcile: mkdir for $dst failed" >&2; exit 1; }
  printf '%s\n' "$desired" > "$dst" || { echo "reconcile: write $dst failed" >&2; exit 1; }
  chmod 644 -- "$dst" || { echo "reconcile: chmod $dst failed" >&2; exit 1; }
  echo "  reconciled $dst"
  CHANGED=1
}

reconcile "HomeMonitoringDashboard.service.d/20-environment.conf" "$DASHBOARD_ENV"

if [[ "$CHANGED" == 1 ]]; then
  systemctl daemon-reload || { echo "reconcile: daemon-reload failed" >&2; exit 1; }
  echo "  systemd daemon reloaded"
else
  echo "  systemd drop-ins already up to date"
fi
