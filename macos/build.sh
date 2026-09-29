#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; VERSION="${VERSION:-1.1.0}"; ARCHS="${ARCHS:-arm64 x86_64}"; BUILD="$ROOT/build/macos"; APP="$BUILD/FlyPet.app"; CONTENTS="$APP/Contents"
rm -rf "$BUILD"; mkdir -p "$CONTENTS/MacOS" "$CONTENTS/Resources" "$BUILD/bin" "$BUILD/dmg" "$ROOT/dist"
cp "$ROOT/macos/Info.plist" "$CONTENTS/Info.plist"; cp "$ROOT/README.md" "$CONTENTS/Resources/使用说明.md"; cp "$ROOT/THIRD_PARTY.md" "$CONTENTS/Resources/THIRD_PARTY.md"; cp "$ROOT/Assets/circuit.json" "$CONTENTS/Resources/circuit.json"
swift "$ROOT/macos/make_icon.swift" "$BUILD/FlyPet.iconset"; iconutil -c icns "$BUILD/FlyPet.iconset" -o "$CONTENTS/Resources/FlyPet.icns"
bins=(); for arch in $ARCHS; do swiftc -O -whole-module-optimization -target "${arch}-apple-macos13.0" -framework AppKit "$ROOT/macos/Sources/FlyPet/main.swift" -o "$BUILD/bin/FlyPet-$arch"; bins+=("$BUILD/bin/FlyPet-$arch"); done
if [ "${#bins[@]}" -eq 1 ]; then cp "${bins[0]}" "$CONTENTS/MacOS/FlyPet"; else lipo -create "${bins[@]}" -output "$CONTENTS/MacOS/FlyPet"; fi
chmod +x "$CONTENTS/MacOS/FlyPet"; codesign --force --deep --sign - "$APP"
cp -R "$APP" "$BUILD/dmg/FlyPet.app"; ln -s /Applications "$BUILD/dmg/Applications"
ARCH_LABEL="$(echo "$ARCHS" | tr ' ' '-')"; DMG="$ROOT/dist/FlyPet-${VERSION}-macOS-${ARCH_LABEL}.dmg"; hdiutil create -volname FlyPet -srcfolder "$BUILD/dmg" -ov -format UDZO "$DMG"
file "$CONTENTS/MacOS/FlyPet"; codesign --verify --deep --strict "$APP"; echo "Created $DMG"
