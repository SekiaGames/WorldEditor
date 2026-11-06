#!/bin/bash
set -euo pipefail

# ---------------------------------------------------------------------------
# Extracts War3 assets (glb + png + .wc3.json) from the game install folder.
# Output tree is Godot/assets/converted, the same one the runtime reads.
# Windows counterpart: extract.bat
#
# Usage: ./extract.command               (also import into Godot afterwards)
#        ./extract.command --no-import   (skip the slow import pass)
#        ./extract.command --force       (ignore the version stamp and extract again)
#
# Steps:
#   1. dotnet build                 the conversions run inside the game binary
#   2. version stamp check          skip everything when the tree is already current
#   3. mdx -> glb + .wc3.json       read straight out of the MPQs
#   4. MPQs -> temp dir tree        (needed by the passes below, deleted after)
#   5. temp dir images -> png
#   6. temp dir wav / mp3 / ttf     copied as is, no decoding
#   7. write version stamp          _extract-version.txt: version + time + counts
#   8. import pass                  rebuilds *.import + .godot/imported, 20-40 min
#
# The version number lives in ONE place: AssetStamp.Version
# (Godot/src/Core/Tools/AssetStamp.cs). Bump it whenever the conversion output
# changes, otherwise stale trees keep being reused.
# ---------------------------------------------------------------------------

# ==== EDIT HERE ============================================================
WAR3_DIR="${WAR3_DIR:-/Users/Shared/War3_1.27a}"
GODOT_BIN="${GODOT_BIN:-}"          # empty = auto-detect Godot 4 mono
# ==========================================================================

IMPORT=1
FORCE=0
for arg in "$@"; do
	case "$arg" in
		--no-import | -noimport) IMPORT=0 ;;
		--force | -force) FORCE=1 ;;
	esac
done

ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT/Godot"
OUT="$PROJECT/assets/converted"

die() {
	printf '\n[ERROR] %s\n' "$1" >&2
	exit 1
}

[ -f "$WAR3_DIR/WAR3.MPQ" ] || [ -f "$WAR3_DIR/war3.mpq" ] \
	|| die "No WAR3.MPQ under: $WAR3_DIR
        Edit WAR3_DIR= at the top of this file, or export WAR3_DIR=/path/to/War3."

if [ -z "$GODOT_BIN" ]; then
	for bin in "/Applications/Godot_mono.app/Contents/MacOS/Godot" "/Applications/Godot.app/Contents/MacOS/Godot"; do
		if [ -x "$bin" ]; then
			GODOT_BIN="$bin"
			break
		fi
	done
fi
if [ -z "$GODOT_BIN" ] && command -v godot >/dev/null 2>&1; then
	GODOT_BIN="$(command -v godot)"
fi
if [ -z "$GODOT_BIN" ]; then
	while IFS= read -r app; do
		if [ -x "$app/Contents/MacOS/Godot" ]; then
			GODOT_BIN="$app/Contents/MacOS/Godot"
			break
		fi
	done <<-EOF
	$(find /Applications "$HOME/Applications" -maxdepth 1 -name 'Godot*.app' 2>/dev/null)
	EOF
fi
[ -n "$GODOT_BIN" ] || die "Godot editor not found. Set GODOT_BIN=/path/to/Godot.app/Contents/MacOS/Godot"

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

printf 'War3   : %s\n' "$WAR3_DIR"
printf 'Godot  : %s\n' "$GODOT_BIN"
printf 'Output : %s\n\n' "$OUT"

printf '[1/7] dotnet build\n'
dotnet build "$PROJECT/WorldEdit.csproj" -v:minimal --nologo

printf '\n[2/7] version stamp check\n'
STATUS_LOG="$(mktemp "${TMPDIR:-/tmp}/worldeditor-asset-status.XXXXXX")"
trap 'rm -f "$STATUS_LOG"' EXIT
"$GODOT_BIN" --headless --path "$PROJECT" -- "--asset-status=$OUT" >"$STATUS_LOG" 2>&1 || true
grep -E '^\[STAMP\]|^STATE=' "$STATUS_LOG" || true
if [ "$FORCE" = "1" ]; then
	:
elif grep -q '^STATE=FRESH$' "$STATUS_LOG"; then
	printf '\n[SKIP] Assets are already at the current version, nothing to extract.\n'
	printf '       Force a full run with: ./extract.command --force\n'
	exit 0
fi

WORK="$(mktemp -d "${TMPDIR:-/tmp}/worldeditor-war3.XXXXXX")"
trap 'rm -rf "$WORK"; rm -f "$STATUS_LOG"' EXIT

printf '\n[3/7] mdx to glb + .wc3.json\n'
"$GODOT_BIN" --headless --path "$PROJECT" -- "--game-dir=$WAR3_DIR" "--mdx-export=$OUT"

printf '\n[4/7] unpacking MPQs to %s\n' "$WORK"
"$GODOT_BIN" --headless --path "$PROJECT" -- "--war3-extract=$WAR3_DIR|$WORK"

printf '\n[5/7] images to png\n'
"$GODOT_BIN" --headless --path "$PROJECT" -- "--image-convert=$WORK|$OUT"

printf '\n[6/7] copying wav / mp3 / ttf\n'
copied=0
while IFS= read -r -d '' file; do
	rel="${file#"$WORK"/}"
	mkdir -p "$OUT/$(dirname "$rel")"
	cp -f "$file" "$OUT/$rel"
	copied=$((copied + 1))
done < <(find "$WORK" -type f \( -iname '*.wav' -o -iname '*.mp3' -o -iname '*.ttf' \) -print0)
printf '      %s files\n' "$copied"

printf '\n[7/7] removing %s and writing the version stamp\n' "$WORK"
rm -rf "$WORK"
"$GODOT_BIN" --headless --path "$PROJECT" -- "--asset-stamp=$OUT|$WAR3_DIR"

if [ "$IMPORT" = "1" ]; then
	printf '\nImporting into Godot, this takes a while...\n'
	"$GODOT_BIN" --headless --path "$PROJECT" --editor --quit || {
		printf '      first pass crashed - the known message-queue overflow near 88%%, running it once more\n'
		"$GODOT_BIN" --headless --path "$PROJECT" --editor --quit
	}
fi

printf '\n[OK] Assets extracted to: %s\n' "$OUT"
