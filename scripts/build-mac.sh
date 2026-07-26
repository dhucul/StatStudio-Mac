#!/usr/bin/env bash
# Build the distributable native macOS app: publish the self-contained .NET engine,
# build the SwiftUI app in release, assemble StatStudio.app (engine embedded), make the
# icon, write Info.plist, and ad-hoc sign. Output: dist/StatStudio.app.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

VERSION="$(sed -nE 's:.*<Version>([^<]+)</Version>.*:\1:p' "$REPO/Directory.Build.props")"
[[ -n "$VERSION" ]] || { echo "Unable to read Version from Directory.Build.props" >&2; exit 1; }
DIST="$REPO/dist"
APP="$DIST/StatStudio.app"
RID="osx-arm64"

rm -rf "$APP" "$DIST/engine-publish"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources/Engine"

echo "==> Publishing engine (self-contained $RID)"
dotnet publish "$REPO/src/StatStudio.Engine/StatStudio.Engine.csproj" \
    -c Release -r "$RID" --self-contained true -p:PublishSingleFile=false \
    -o "$DIST/engine-publish" | tail -2

echo "==> Building SwiftUI app (release)"
( cd "$REPO/mac/StatStudio" && swift build -c release )

echo "==> Assembling bundle"
cp "$REPO/mac/StatStudio/.build/release/StatStudio" "$APP/Contents/MacOS/StatStudio"
cp -R "$DIST/engine-publish/." "$APP/Contents/Resources/Engine/"
chmod +x "$APP/Contents/Resources/Engine/StatStudio.Engine"

bash "$REPO/scripts/make-icns.sh" "$REPO/assets/icon-preview.png" "$APP/Contents/Resources/StatStudio.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>StatStudio</string>
    <key>CFBundleDisplayName</key><string>StatStudio</string>
    <key>CFBundleIdentifier</key><string>ca.eastlink.statstudio</string>
    <key>CFBundleExecutable</key><string>StatStudio</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>$VERSION</string>
    <key>CFBundleIconFile</key><string>StatStudio</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>LSApplicationCategoryType</key><string>public.app-category.education</string>
</dict>
</plist>
PLIST

printf 'APPL????' > "$APP/Contents/PkgInfo"

echo "==> Ad-hoc signing"
codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || echo "   (ad-hoc sign skipped/failed — app still runs locally)"

if [[ "${1:-}" == "--dmg" ]]; then
    echo "==> Building .dmg"
    hdiutil create -volname StatStudio -srcfolder "$APP" -ov -format UDZO "$DIST/StatStudio.dmg" | tail -1
fi

echo
echo "Built: $APP"
echo "Run:   open '$APP'    (first launch: right-click ▸ Open to clear Gatekeeper)"
