#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$SCRIPT_DIR"

echo "=== GDD macOS Setup ==="
echo "Directory: $SCRIPT_DIR"

# 1. Make executable
chmod +x GDD.Headless 2>/dev/null || true
find . -name "*.dylib" -exec chmod +x {} \; 2>/dev/null || true

# 2. Remove quarantine
echo "Removing macOS quarantine attributes..."
xattr -dr com.apple.quarantine . 2>/dev/null || true

# 3. Check/install Chromium
BROWSERS_DIR="$SCRIPT_DIR/.browsers"
if find "$BROWSERS_DIR" -name "headless_shell" -o -name "chrome" 2>/dev/null | grep -q .; then
    echo "Chromium: already installed"
else
    echo "Chromium: not found, installing..."

    # Detect platform
    ARCH="$(uname -m)"
    if [ "$ARCH" = "arm64" ]; then
        NODE_PLATFORM="darwin-arm64"
    else
        NODE_PLATFORM="darwin-x64"
    fi

    NODE="$SCRIPT_DIR/.playwright/node/$NODE_PLATFORM/node"
    CLI="$SCRIPT_DIR/.playwright/package/cli.js"

    if [ -x "$NODE" ] && [ -f "$CLI" ]; then
        PLAYWRIGHT_BROWSERS_PATH="$BROWSERS_DIR" "$NODE" "$CLI" install chromium
        echo "Removing quarantine from installed browsers..."
        xattr -dr com.apple.quarantine .browsers .playwright 2>/dev/null || true
    else
        echo "ERROR: Bundled Node.js not found at $NODE"
        echo "Manual install:"
        echo "  PLAYWRIGHT_BROWSERS_PATH=\"\$(pwd)/.browsers\" \\"
        echo "    ./.playwright/node/$NODE_PLATFORM/node \\"
        echo "    ./.playwright/package/cli.js install chromium"
        exit 1
    fi
fi

echo ""
echo "=== Setup complete ==="
echo "Run GDD:  ./GDD.Headless"
echo "Headless: ./GDD.Headless --headless"
