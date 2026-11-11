#!/bin/zsh
set -e
cd "$(dirname "$0")/Launcher"

GODOT="${GODOT_BIN:-$HOME/Applications/Godot_mono.app/Contents/MacOS/Godot}"
if [ ! -x "$GODOT" ]; then
  GODOT="/Applications/Godot_mono.app/Contents/MacOS/Godot"
fi
if [ ! -x "$GODOT" ]; then
  echo "找不到 Godot_mono：请设置 GODOT_BIN 环境变量"
  exit 1
fi

dotnet build Launcher.csproj -v q --nologo
exec "$GODOT" --path . "$@"