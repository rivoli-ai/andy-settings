#!/usr/bin/env bash
# Pack Andy.Settings.Client + Andy.Settings.Client.TestSupport into ./artifacts/
# with a fixed dev version, for consumption by sibling andy-* repos during
# epic-branch development (before the CI workflow publishes a real rc to
# nuget.org on merge to main).
#
# Consumer repos resolve these packages via a root-level nuget.config that
# declares `../andy-settings/artifacts` as a package source.
#
# Version is pinned at 0.1.0-dev so every consumer's Directory.Packages.props
# can reference the same constant during development. On merge to main, flip
# consumer pins to the real `yyyy.mm.dd-rc.<run>` version the CI publishes.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

VERSION="${ANDY_SETTINGS_CLIENT_DEV_VERSION:-0.1.0-dev}"
OUT="$REPO_ROOT/artifacts"

echo "==> Packing Andy.Settings.Client @ $VERSION -> $OUT"
rm -f "$OUT"/Andy.Settings.Client.*.nupkg "$OUT"/Andy.Settings.Client.*.snupkg
mkdir -p "$OUT"

for project in \
  src/Andy.Settings.Client/Andy.Settings.Client.csproj \
  src/Andy.Settings.Client.TestSupport/Andy.Settings.Client.TestSupport.csproj; do
  dotnet pack "$project" \
    --configuration Release \
    --output "$OUT" \
    -p:PackageVersion="$VERSION"
done

echo
echo "==> Packed:"
ls -1 "$OUT"/*.nupkg
