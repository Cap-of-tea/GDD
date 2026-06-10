#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$SCRIPT_DIR"

echo "=== GDD.Desktop macOS Setup ==="
echo "Directory: $SCRIPT_DIR"

# 1. Make executable
chmod +x GDD.Desktop 2>/dev/null || true
find . -name "*.dylib" -exec chmod +x {} \; 2>/dev/null || true

# 2. Remove quarantine (Gatekeeper) from the app and bundled tooling
echo "Removing macOS quarantine attributes..."
xattr -dr com.apple.quarantine . 2>/dev/null || true

# 3. Ad-hoc codesign so Gatekeeper allows launch
echo "Ad-hoc signing GDD.Desktop..."
codesign --force --deep --sign - GDD.Desktop 2>/dev/null || true

# 4. Check/install Chromium
BROWSERS_DIR="$SCRIPT_DIR/.browsers"
if find "$BROWSERS_DIR" -name "headless_shell" -o -name "chrome" 2>/dev/null | grep -q .; then
    echo "Chromium: already installed"
else
    echo "Chromium: not found — it will download automatically on first launch."
fi

echo
echo "=== Setup complete ==="
echo "Run GDD.Desktop:  ./GDD.Desktop"
echo "Note: macOS will ask for Screen Recording permission only if you later add native window capture."
