#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PRESET="macOS"
APP_NAME="War3 World Editor"
OUT="$ROOT/$APP_NAME.app"

die() {
	printf '\n[ERROR] %s\n' "$1" >&2
	exit 1
}

printf 'Project: %s\n' "$ROOT"

if ! command -v dotnet >/dev/null 2>&1; then
	for dir in /usr/local/share/dotnet /opt/homebrew/bin /opt/homebrew/opt/dotnet/libexec "$HOME/.dotnet"; do
		if [ -x "$dir/dotnet" ]; then
			PATH="$dir:$PATH"
			export PATH
			break
		fi
	done
fi
command -v dotnet >/dev/null 2>&1 || die "dotnet SDK not found. Install .NET SDK and retry."

GODOT="${GODOT_BIN:-}"
if [ -z "$GODOT" ]; then
	for bin in "/Applications/Godot_mono.app/Contents/MacOS/Godot" "/Applications/Godot.app/Contents/MacOS/Godot"; do
		if [ -x "$bin" ]; then
			GODOT="$bin"
			break
		fi
	done
fi
if [ -z "$GODOT" ] && command -v godot >/dev/null 2>&1; then
	GODOT="$(command -v godot)"
fi
if [ -z "$GODOT" ]; then
	while IFS= read -r app; do
		if [ -x "$app/Contents/MacOS/Godot" ]; then
			GODOT="$app/Contents/MacOS/Godot"
			break
		fi
	done <<-EOF
	$(find /Applications "$HOME/Applications" -maxdepth 1 -name 'Godot*.app' 2>/dev/null)
	EOF
fi
[ -n "$GODOT" ] || die "Godot editor not found. Set GODOT_BIN=/path/to/Godot.app/Contents/MacOS/Godot"

VERSION_STRING="$("$GODOT" --version 2>/dev/null | tail -1)"
TEMPLATE_VERSION="$(printf '%s' "$VERSION_STRING" | sed 's/\.official\..*$//')"
TEMPLATES="$HOME/Library/Application Support/Godot/export_templates/$TEMPLATE_VERSION"
printf 'Godot:   %s (%s)\n' "$GODOT" "$VERSION_STRING"
[ -f "$TEMPLATES/macos.zip" ] || die "macOS export templates missing: $TEMPLATES/macos.zip"

printf 'dotnet:  %s\n' "$(command -v dotnet)"
printf 'Signing: %s\n\n' "$(grep -m1 '^codesign/codesign=' "$ROOT/export_presets.cfg" 2>/dev/null || echo 'n/a')"

if [ -e "$OUT" ]; then
	printf 'Removing previous bundle: %s\n' "$OUT"
	rm -rf "$OUT"
fi

printf 'Exporting preset "%s"...\n\n' "$PRESET"
if ! "$GODOT" --headless --path "$ROOT" --export-release "$PRESET" "$OUT"; then
	die "Export failed, see the log above."
fi

[ -d "$OUT" ] || die "Export reported success but no bundle at: $OUT"
[ -x "$OUT/Contents/MacOS/$APP_NAME" ] || die "Bundle is incomplete, missing executable: $OUT/Contents/MacOS/$APP_NAME"

printf '\n[OK] Done. Output: %s\n' "$OUT"
printf '     Size: %s\n' "$(du -sh "$OUT" | cut -f1)"
printf '     Run:  open "%s"\n' "$OUT"
