#!/bin/bash
# Usage: ./make-dmg.sh <app-bundle-dir> <version>
set -e
APP_BUNDLE="$1"
VERSION="$2"
DMG_NAME="Pix2d_macOS-$VERSION.dmg"
VOLUME_NAME="Pix2D Installer"

# Create a temporary staging directory
STAGING_DIR="dmg-staging"
rm -rf "$STAGING_DIR"
mkdir "$STAGING_DIR"

# Copy the .app bundle
cp -R "$APP_BUNDLE" "$STAGING_DIR/"

# Create Applications symlink
ln -s /Applications "$STAGING_DIR/Applications"

# Optional: set a background image (uncomment and provide path if needed)
# mkdir "$STAGING_DIR/.background"
# cp /path/to/background.png "$STAGING_DIR/.background/background.png"

# Create the DMG
hdiutil create -volname "$VOLUME_NAME" -srcfolder "$STAGING_DIR" -ov -format UDZO "$DMG_NAME"

# Clean up
rm -rf "$STAGING_DIR"

echo "DMG created: $DMG_NAME"
