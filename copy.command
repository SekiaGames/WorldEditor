#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
SOURCE="$ROOT/Launcher"

[ -d "$SOURCE" ] || { printf '[ERROR] 找不到目录 %s\n' "$SOURCE" >&2; exit 1; }

for index in {1..10}; do
	target="$ROOT/Launcher$index"
	printf '[%s/10] %s\n' "$index" "$target"
	rm -rf "$target"
	cp -R "$SOURCE" "$target"
done

printf '\n[OK] Launcher 已复制为 Launcher1 到 Launcher10\n'