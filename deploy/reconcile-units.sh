#!/usr/bin/env bash
#
# Ensure the systemd drop-ins that carry host settings a deploy must not lose exist — currently the
# dashboard's ASPNETCORE_URLS bind. deploy.sh runs this each deploy so a missing/wiped drop-in
# self-heals. It does NOT overwrite a drop-in that is already present, so a manual change is
# preserved; deploy.sh additionally verifies at runtime that the dashboard actually bound the
# expected address, which catches a present-but-wrong drop-in.
#
# SECURITY: the content is SYNTHESIZED below, never copied from a caller-supplied path. This helper
# runs as root via NOPASSWD sudo; copying an arbitrary source would let a compromised runner install
# malicious unit directives (e.g. User=root / ExecStart=...) or symlink root-only files into a
# world-readable drop-in. Command-line arguments are therefore IGNORED (one is still accepted so the
# `hm-reconcile-units *` sudoers pattern keeps matching).
#
# The embedded content is the create-if-missing DEFAULT; keep it byte-identical to
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

ensure_present() { # <relative-path-under-DEST> <content-when-creating>
  local dst="$DEST/$1" desired="$2"
  if [[ -f "$dst" ]]; then
    return 0   # present → leave as-is (never clobber a manual/updated drop-in)
  fi
  mkdir -p -- "$(dirname -- "$dst")" || { echo "reconcile: mkdir for $dst failed" >&2; exit 1; }
  printf '%s\n' "$desired" > "$dst" || { echo "reconcile: write $dst failed" >&2; exit 1; }
  chmod 644 -- "$dst" || { echo "reconcile: chmod $dst failed" >&2; exit 1; }
  echo "  created $dst"
  CHANGED=1
}

ensure_present "HomeMonitoringDashboard.service.d/20-environment.conf" "$DASHBOARD_ENV"

if [[ "$CHANGED" == 1 ]]; then
  systemctl daemon-reload || { echo "reconcile: daemon-reload failed" >&2; exit 1; }
  echo "  systemd daemon reloaded"
else
  echo "  systemd drop-ins already present"
fi
