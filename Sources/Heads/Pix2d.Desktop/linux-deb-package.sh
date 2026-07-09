#!/bin/bash
# Builds a Debian package (.deb) for Pix2D from a self-contained linux publish output.
#
# Usage: ./linux-deb-package.sh <publish-dir> <version> [arch]
#   <publish-dir>  directory produced by `dotnet publish -r linux-x64 --self-contained true`
#   <version>      e.g. 3.8.2
#   [arch]         dpkg architecture, default amd64 (use arm64 for linux-arm64 builds)
#
# Produces: pix2d_<version>_<arch>.deb in the current directory.
# Requires: dpkg-deb (dpkg). Optionally ImageMagick `convert` for multi-size icons.
#
# The package installs the app to /opt/pix2d, a launcher at /usr/bin/pix2d, a
# Cinnamon/GNOME/KDE menu entry, hicolor icons, and a .pix2d MIME association.
set -euo pipefail

PUBLISH_DIR="$1"
VERSION="$2"
ARCH="${3:-amd64}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PKG_NAME="pix2d"
STAGING="deb-staging"
INSTALL_PREFIX="opt/pix2d"
DEB_FILE="${PKG_NAME}_${VERSION}_${ARCH}.deb"

# Icon source — the same design asset the macOS .icns pipeline uses. Override with ICON_SRC.
ICON_SRC="${ICON_SRC:-DesignAssets/Artboard 1 – 4.png}"

echo "==> Building $DEB_FILE from $PUBLISH_DIR (arch=$ARCH)"

rm -rf "$STAGING" "$DEB_FILE"
mkdir -p "$STAGING/DEBIAN"
mkdir -p "$STAGING/$INSTALL_PREFIX"
mkdir -p "$STAGING/usr/bin"
mkdir -p "$STAGING/usr/share/applications"
mkdir -p "$STAGING/usr/share/mime/packages"

# 1. Application payload -> /opt/pix2d
cp -R "$PUBLISH_DIR"/. "$STAGING/$INSTALL_PREFIX/"
chmod +x "$STAGING/$INSTALL_PREFIX/Pix2d"

# 2. Launcher on PATH -> /usr/bin/pix2d (wrapper keeps the app self-locating regardless of cwd)
cat > "$STAGING/usr/bin/pix2d" <<'EOF'
#!/bin/sh
exec /opt/pix2d/Pix2d "$@"
EOF
chmod +x "$STAGING/usr/bin/pix2d"

# 3. Desktop entry + MIME type
cp "$SCRIPT_DIR/pix2d.desktop" "$STAGING/usr/share/applications/pix2d.desktop"
cp "$SCRIPT_DIR/pix2d.mime.xml" "$STAGING/usr/share/mime/packages/pix2d.xml"

# 4. Icons — resize the shared design asset to standard hicolor sizes if ImageMagick is present.
if command -v convert >/dev/null 2>&1 && [ -f "$ICON_SRC" ]; then
  for size in 512 256 128 64 48; do
    dir="$STAGING/usr/share/icons/hicolor/${size}x${size}/apps"
    mkdir -p "$dir"
    convert "$ICON_SRC" -resize "${size}x${size}" "$dir/pix2d.png"
  done
elif [ -f "$ICON_SRC" ]; then
  echo "WARN: ImageMagick 'convert' not found; installing the icon as-is under 512x512"
  dir="$STAGING/usr/share/icons/hicolor/512x512/apps"
  mkdir -p "$dir"
  cp "$ICON_SRC" "$dir/pix2d.png"
else
  echo "WARN: icon source '$ICON_SRC' not found; package will have no application icon"
fi

# 5. control
INSTALLED_SIZE=$(du -k -s "$STAGING" | cut -f1)
cat > "$STAGING/DEBIAN/control" <<EOF
Package: $PKG_NAME
Version: $VERSION
Section: graphics
Priority: optional
Architecture: $ARCH
Maintainer: Pix2D <support@pix2d.com>
Homepage: https://pix2d.com
Installed-Size: $INSTALLED_SIZE
Depends: libc6, libgcc-s1, libstdc++6, libx11-6, libice6, libsm6, libfontconfig1, zlib1g
Description: Animated sprite and pixel art editor
 Pix2D is a cross-platform animated sprite, pixel-art and animation editor
 for indie game developers and pixel artists. Self-contained build — no
 separate .NET runtime install required.
EOF

# 6. Maintainer scripts — refresh desktop / icon / MIME caches so the menu entry and
#    .pix2d association appear immediately after install and vanish after removal.
write_cache_refresh() {
  cat > "$1" <<'EOF'
#!/bin/sh
set -e
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q /usr/share/applications || true
command -v update-mime-database    >/dev/null 2>&1 && update-mime-database /usr/share/mime            || true
command -v gtk-update-icon-cache   >/dev/null 2>&1 && gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
exit 0
EOF
  chmod 0755 "$1"
}
write_cache_refresh "$STAGING/DEBIAN/postinst"
write_cache_refresh "$STAGING/DEBIAN/postrm"

# 7. Build (root:root ownership without needing fakeroot/sudo)
dpkg-deb --build --root-owner-group "$STAGING" "$DEB_FILE"
rm -rf "$STAGING"

echo "==> Created $DEB_FILE"
