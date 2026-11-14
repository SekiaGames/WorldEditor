#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"

# ==== EDIT HERE ============================================================
WAR3_DIR="${WAR3_DIR:-$ROOT/test/War3_1.27a}"
GODOT="${GODOT:-/Volumes/Data/Users/sekia/Applications/Godot_mono.app/Contents/MacOS/Godot}"
# ==========================================================================

PROJECT="$ROOT/Extract"
OUT="$PROJECT/extract"
WORK="$ROOT/test/war3-unpack"
PACK="$ROOT/extract.pck"
UNPACK_MARKER=".unpacked"

WAR3_DIR="${WAR3_DIR%/}"
GD_ARGS=(-- "--game-dir=$WAR3_DIR")

die() {
	printf '\n[ERROR] %s\n' "$1" >&2
	exit 1
}

# Godot 偶发在进程销毁阶段崩（文件已写完、退出码非 0），每步允许重试一次；转换步骤增量跳过，重试只补缺口。
run_step() {
	local label="$1"
	shift
	if "$GODOT" --headless --quiet --path "$PROJECT" "$@"; then
		return 0
	fi
	printf '      %s failed - retrying once\n' "$label"
	"$GODOT" --headless --quiet --path "$PROJECT" "$@"
}

if [ ! -x "$GODOT" ]; then
	GODOT=""
	for app in "/Applications/Godot_mono.app/Contents/MacOS/Godot" \
		"$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot" \
		"/Applications/Godot.app/Contents/MacOS/Godot"; do
		if [ -x "$app" ]; then
			GODOT="$app"
			break
		fi
	done
fi
[ -n "$GODOT" ] || die "Godot mono not found. Set GODOT=/path/to/Godot_mono.app/Contents/MacOS/Godot"

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

[ -d "$WAR3_DIR" ] || die "No such directory: $WAR3_DIR"
[ -f "$WAR3_DIR/war3.mpq" ] || die "No war3.mpq under: $WAR3_DIR"

printf 'War3   : %s\n' "$WAR3_DIR"
printf 'Godot  : %s\n' "$GODOT"
printf 'Output : %s\n' "$OUT"
printf 'Pack   : %s\n\n' "$PACK"

printf '[1/8] dotnet build\n'
dotnet build "$PROJECT/Extract.csproj" -v:minimal --nologo

printf '\n[2/8] unpack mpq to %s\n' "$WORK"
mkdir -p "$WORK" "$OUT"
rm -rf "$OUT/Tables" "$OUT/Texts"
if [ -f "$WORK/$UNPACK_MARKER" ]; then
	printf '      marker found, skip unpack\n'
else
	run_step unpack "${GD_ARGS[@]}" "--war3-extract=$WAR3_DIR|$WORK"
	: > "$WORK/$UNPACK_MARKER"
fi

printf '\n[3/8] images to png\n'
run_step images "${GD_ARGS[@]}" "--image-convert=$WORK|$OUT"

printf '\n[4/8] mdx to glb\n'
run_step mdx "${GD_ARGS[@]}" "--source-dir=$WORK" "--mdx-export=$OUT"

printf '\n[5/8] copy audio\n'
run_step media "${GD_ARGS[@]}" "--media-copy=$WORK|$OUT"

printf '\n[6/8] importing into godot, this takes 20 to 40 minutes\n'
if ! "$GODOT" --headless --quiet --path "$PROJECT" --import; then
	printf '      first pass failed - retrying once\n'
	"$GODOT" --headless --quiet --path "$PROJECT" --import
fi

printf '\n[7/8] config to C#: tables, texts, fdf, lighting\n'
mkdir -p "$ROOT/Launcher/Generated"
run_step tables "${GD_ARGS[@]}" "--source-dir=$WORK" "--table-codegen=$ROOT/Launcher/Generated"
run_step texts "${GD_ARGS[@]}" "--source-dir=$WORK" "--text-codegen=$ROOT/Launcher/Generated"
run_step fdf "${GD_ARGS[@]}" "--source-dir=$WORK" "--fdf-codegen=$ROOT/Launcher/Generated"
run_step lighting "${GD_ARGS[@]}" "--lighting-codegen=$WORK|$ROOT/Launcher/Generated"

printf '\n[8/8] pack %s\n' "$PACK"
rm -f "$PACK"
run_step pack --export-pack "extract.pck" "$PACK"

printf '\n[OK] resources %s\n' "$OUT"
printf '     pack      %s\n' "$PACK"
printf '     copy the pack next to Editor\n\n'
