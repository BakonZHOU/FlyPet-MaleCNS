#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
TARGET="${1:-macos-universal}"
case "$TARGET" in
  macos-arm64) ARCHS="arm64" ;;
  macos-x64) ARCHS="x86_64" ;;
  macos-universal) ARCHS="arm64 x86_64" ;;
  *) echo "Usage: ./package.sh {macos-arm64|macos-x64|macos-universal}" >&2; exit 2 ;;
esac
export ARCHS
exec "$ROOT/macos/build.sh"
