#!/bin/bash
set -uo pipefail
cd "$(dirname "$0")/Open-Warcraft3_Wiki" || exit 1

TFM=$(sed -n 's:.*<TargetFramework>\([^<]*\)</TargetFramework>.*:\1:p' VisualStudio/OpenWC3.Game.csproj | head -n 1)
DEBUG_DIR="VisualStudio/bin/Debug/$TFM"
DEBUG_BIN="$DEBUG_DIR/OpenWC3.Game"
PUBLISH_BIN=".Building/osx/OpenWC3.Game"

GAME=${OPENWC3_GAME_BIN:-}
if [ -z "$GAME" ]; then
  if [ -x "$DEBUG_BIN" ]; then GAME=$DEBUG_BIN
  elif [ -x "$PUBLISH_BIN" ]; then GAME=$PUBLISH_BIN
  fi
fi

pause() {
  echo
  read -n 1 -s -r -p "按任意键关闭窗口..." || true
  echo
}

if [ -z "$GAME" ] || [ ! -x "$GAME" ]; then
  echo "[ERROR] 找不到可执行文件。"
  echo "  找过: $DEBUG_BIN"
  echo "        $PUBLISH_BIN"
  echo "先运行 Mac/build_osx.command 选 1 编译一次。"
  pause
  exit 1
fi

echo "OpenWC3"
echo "  工作目录: $(pwd)"
echo "  可执行  : $GAME"
if [ "$#" -gt 0 ]; then
  echo "  参数    : $*"
fi
echo

"$GAME" "$@"
RC=$?

if [ "$RC" -ne 0 ]; then
  echo
  echo "[FAIL] 退出码 $RC"
  if [ -d "$DEBUG_DIR/logs" ]; then
    echo "  崩溃日志: $(pwd)/$DEBUG_DIR/logs/"
  fi
  pause
fi

exit "$RC"
