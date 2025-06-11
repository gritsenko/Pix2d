#!/bin/bash
# Usage: ./macos-app-bundle.sh <publish-dir> <version>
set -e
PUBLISH_DIR="$1"
VERSION="$2"
APP_NAME="Pix2D"
BUNDLE_DIR="Pix2D.app/Contents"

# Clean up any previous bundle
rm -rf "Pix2D.app"

# Create bundle structure
mkdir -p "$BUNDLE_DIR/MacOS"
mkdir -p "$BUNDLE_DIR/Resources"

# Copy main binary and all published files
cp -R "$PUBLISH_DIR"/* "$BUNDLE_DIR/MacOS/"

# Copy Info.plist
cp "$(dirname "$0")/Info.plist" "$BUNDLE_DIR/Info.plist"

# Copy icon (must be .icns)
cp "$(dirname "$0")/Pix2d.icns" "$BUNDLE_DIR/Resources/Pix2d.icns"

# Set executable permissions
chmod +x "$BUNDLE_DIR/MacOS/Pix2d"

# Set version in Info.plist
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$BUNDLE_DIR/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$BUNDLE_DIR/Info.plist"

echo "App bundle created: Pix2D.app"
