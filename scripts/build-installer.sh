#!/usr/bin/env bash
# Build a macOS .pkg installer that installs StatStudio.app into /Applications.
# Double-clicking the result runs the standard macOS Installer wizard.
#
# Usage:
#   scripts/build-installer.sh            # build the installer (builds the app if missing)
#   scripts/build-installer.sh --rebuild  # force-rebuild the .app first
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST="$REPO/dist"
APP="$DIST/StatStudio.app"
VERSION="2.1.0"
ID="ca.eastlink.statstudio"

if [[ "${1:-}" == "--rebuild" || ! -d "$APP" ]]; then
    echo "==> Building StatStudio.app"
    bash "$REPO/scripts/build-mac.sh"
fi

echo "==> Staging app for packaging"
ROOT="$DIST/pkgroot"
RES="$DIST/pkgres"
rm -rf "$ROOT" "$RES"
mkdir -p "$ROOT" "$RES"
cp -R "$APP" "$ROOT/StatStudio.app"

# A short welcome shown by the Installer.
cat > "$RES/welcome.html" <<'HTML'
<html><body style="font-family:-apple-system,Helvetica,sans-serif">
<h2>StatStudio</h2>
<p>A Minitab-style statistics workbench, native for Apple Silicon.</p>
<p>This will install <b>StatStudio.app</b> into your <b>Applications</b> folder.
The statistics engine is bundled inside the app — no other software is required.</p>
</body></html>
HTML

echo "==> pkgbuild (component → /Applications)"
pkgbuild --root "$ROOT" --install-location /Applications \
    --identifier "$ID" --version "$VERSION" \
    "$DIST/StatStudio-component.pkg" >/dev/null

echo "==> productbuild (installer wizard)"
DISTXML="$DIST/distribution.xml"
cat > "$DISTXML" <<XML
<?xml version="1.0" encoding="utf-8"?>
<installer-gui-script minSpecVersion="1">
    <title>StatStudio $VERSION</title>
    <welcome file="welcome.html"/>
    <options customize="never" require-scripts="false" hostArchitectures="arm64"/>
    <domains enable_localSystem="true"/>
    <choices-outline><line choice="default"/></choices-outline>
    <choice id="default" title="StatStudio"><pkg-ref id="$ID"/></choice>
    <pkg-ref id="$ID" version="$VERSION">StatStudio-component.pkg</pkg-ref>
</installer-gui-script>
XML

productbuild --distribution "$DISTXML" --resources "$RES" \
    --package-path "$DIST" \
    "$DIST/StatStudioInstaller.pkg" >/dev/null

rm -f "$DIST/StatStudio-component.pkg" "$DISTXML"
rm -rf "$ROOT" "$RES"

echo
echo "Built: $DIST/StatStudioInstaller.pkg"
echo "Install: double-click it (first time: right-click ▸ Open to clear Gatekeeper)."
echo "         The wizard installs StatStudio.app into /Applications."
