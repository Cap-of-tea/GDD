#!/usr/bin/env bash
# Install runtime dependencies for GDD.Desktop on Debian/Ubuntu:
#  - Avalonia (X11 + Skia/HarfBuzz font rendering)
#  - Chromium (Playwright) shared libraries
# Chromium itself is downloaded automatically on first launch.
set -e

SUDO=""
if [ "$(id -u)" -ne 0 ]; then SUDO="sudo"; fi

echo "Installing GDD.Desktop runtime dependencies..."
$SUDO apt-get update
$SUDO apt-get install -y \
    libx11-6 libice6 libsm6 libfontconfig1 libxrandr2 libxi6 libxcursor1 \
    libxext6 libxrender1 libgl1 libglib2.0-0 \
    libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 libxkbcommon0 \
    libxcomposite1 libxdamage1 libxfixes3 libgbm1 libpango-1.0-0 libcairo2 libasound2 \
    curl ca-certificates

echo
echo "Dependencies installed. Chromium downloads on first launch."
echo "Run GDD.Desktop with:  ./GDD.Desktop"
