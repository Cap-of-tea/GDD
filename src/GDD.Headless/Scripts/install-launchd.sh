#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
GDD_EXE="$SCRIPT_DIR/GDD.Headless"
LOGS_DIR="$SCRIPT_DIR/logs"
PLIST_PATH="$HOME/Library/LaunchAgents/com.gdd.headless.plist"
LABEL="com.gdd.headless"

if [ "$(uname)" != "Darwin" ]; then
    echo "This script is for macOS only. On Linux, use systemd:"
    echo "  See README.md → 'Linux: Autostart via systemd'"
    exit 1
fi

if [ ! -x "$GDD_EXE" ]; then
    echo "GDD.Headless not found at $GDD_EXE"
    echo "Run setup-macos.sh first."
    exit 1
fi

# Parse args
GDD_MODE=""
ACTION="install"
for arg in "$@"; do
    case "$arg" in
        --headless) GDD_MODE="--headless" ;;
        --uninstall|--remove) ACTION="uninstall" ;;
    esac
done

if [ "$ACTION" = "uninstall" ]; then
    if [ -f "$PLIST_PATH" ]; then
        launchctl unload "$PLIST_PATH" 2>/dev/null || true
        rm -f "$PLIST_PATH"
        echo "GDD launchd service removed."
    else
        echo "No GDD launchd service found."
    fi
    exit 0
fi

# Build ProgramArguments
PROGRAM_ARGS="        <string>$GDD_EXE</string>"
if [ -n "$GDD_MODE" ]; then
    PROGRAM_ARGS="$PROGRAM_ARGS
        <string>$GDD_MODE</string>"
fi

# Create logs directory
mkdir -p "$LOGS_DIR"

# Unload existing service if present
if [ -f "$PLIST_PATH" ]; then
    launchctl unload "$PLIST_PATH" 2>/dev/null || true
fi

# Write plist
mkdir -p "$(dirname "$PLIST_PATH")"
cat > "$PLIST_PATH" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>$LABEL</string>
    <key>ProgramArguments</key>
    <array>
$PROGRAM_ARGS
    </array>
    <key>WorkingDirectory</key>
    <string>$SCRIPT_DIR</string>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>$LOGS_DIR/launchd-stdout.log</string>
    <key>StandardErrorPath</key>
    <string>$LOGS_DIR/launchd-stderr.log</string>
</dict>
</plist>
EOF

# Load service
launchctl load "$PLIST_PATH"

echo "=== GDD launchd service installed ==="
echo "Plist:  $PLIST_PATH"
echo "Mode:   ${GDD_MODE:-headed (default)}"
echo "Logs:   $LOGS_DIR/launchd-*.log"
echo ""
echo "GDD is now running and will auto-start at login."
echo ""
echo "Manage:"
echo "  launchctl unload $PLIST_PATH   # stop"
echo "  launchctl load $PLIST_PATH     # start"
echo "  launchctl list | grep gdd      # status"
echo ""
echo "Uninstall:"
echo "  ./Scripts/install-launchd.sh --uninstall"
echo ""
echo "Connect Claude Code / Cursor — add to .mcp.json:"
echo '  { "mcpServers": { "gdd": { "url": "http://localhost:9700/mcp" } } }'
