#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_NAME="Jellyfin.Plugin.HLSDownloader"
SOLUTION_PATH="$ROOT_DIR/${PLUGIN_NAME}.sln"
PUBLISH_DIR="$ROOT_DIR/${PLUGIN_NAME}/bin/Debug/net9.0/publish"
TARGET_DIR="/var/lib/jellyfin/plugins/${PLUGIN_NAME}"

echo "[1/6] Build + publish (${SOLUTION_PATH})"
dotnet publish --configuration=Debug "$SOLUTION_PATH" /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary

echo "[2/6] Create target dir as root"
sudo mkdir -p "$TARGET_DIR"

echo "[3/6] Clean old plugin files in target"
sudo find "$TARGET_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf {} +

echo "[4/6] Copy published files as root"
sudo cp -a "$PUBLISH_DIR/." "$TARGET_DIR/"

echo "[5/6] Remove runtime folders that break Jellyfin on Linux"
sudo find "$TARGET_DIR/runtimes" -maxdepth 1 -type d \( -name 'win-*' -o -name 'browser*' \) -exec rm -rf {} + || true
sudo rm -f "$TARGET_DIR/meta.json"

echo "[6/6] Set ownership to jellyfin:jellyfin"
sudo chown -R jellyfin:jellyfin "$TARGET_DIR"

echo "[7/7] Restart jellyfin service"
sudo systemctl restart jellyfin

echo "Done: deployed to $TARGET_DIR and restarted jellyfin"
