#!/bin/bash
set -uo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
GMAP="${1:?gmap}"
W3X="${2:?w3x}"
WAIT="${3:-12}"
OUT="${4:-/tmp/sbs}"
mkdir -p "$OUT"

GODOT="$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot"

"$GODOT" --path "$ROOT/Launcher" -- --play="$GMAP" --shot="$OUT/launcher.png@$WAIT" >"$OUT/launcher.log" 2>&1

(cd "$ROOT/Open-Warcraft3_Wiki" && OPENWC3_SHOT="$OUT/client.png" OPENWC3_SHOT_WAIT="$WAIT" \
  ./VisualStudio/bin/Debug/net11.0/OpenWC3.Game "$W3X" >"$OUT/client.log" 2>&1)

ls -l "$OUT"/launcher.png "$OUT"/client.png
