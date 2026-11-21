#!/usr/bin/env sh
# PCLFE Linux Edition 安装器。
# 只做用户级安装（不写系统目录、不调用 sudo），依赖需要用户自行安装。

set -u

SRC_DIR=$(cd "$(dirname "$0")" && pwd -P)

# shellcheck source=lib/common.sh
. "$SRC_DIR/lib/common.sh"
# shellcheck source=lib/deps.sh
. "$SRC_DIR/lib/deps.sh"

TARGET_HOME=${PCLFE_HOME:-${XDG_DATA_HOME:-$HOME/.local/share}/pclfe-linux}
BIN_DIR=${PCLFE_BIN_DIR:-$HOME/.local/bin}
DATA_HOME=${XDG_DATA_HOME:-$HOME/.local/share}
DO_DEPS=1
DO_SETUP=1
FROM_PATH=''

usage() {
    cat <<EOF
$PCLFE_NAME 安装器

用法：./linux/install.sh [选项]

选项：
  --prefix DIR       安装到指定目录（默认 $TARGET_HOME）
  --bin DIR          创建命令链接的目录（默认 $BIN_DIR）
  --from PATH        安装时直接从 .exe / .zip / 目录复制启动器本体
  --no-deps          跳过依赖检查
  --no-setup         跳过 Wine 前缀初始化（稍后可执行 pclfe setup）
  --help             显示本帮助

安装完成后：
  pclfe setup    初始化 Wine 前缀与 .NET Framework 4.8
  pclfe update   下载启动器本体
  pclfe run      启动
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --prefix) [ $# -ge 2 ] || die "--prefix 需要参数"; TARGET_HOME=$2; shift ;;
        --prefix=*) TARGET_HOME=${1#--prefix=} ;;
        --bin) [ $# -ge 2 ] || die "--bin 需要参数"; BIN_DIR=$2; shift ;;
        --bin=*) BIN_DIR=${1#--bin=} ;;
        --from) [ $# -ge 2 ] || die "--from 需要参数"; FROM_PATH=$2; shift ;;
        --from=*) FROM_PATH=${1#--from=} ;;
        --no-deps) DO_DEPS=0 ;;
        --no-setup) DO_SETUP=0 ;;
        --help|-h) usage; exit 0 ;;
        *) err "未知参数：$1"; usage; exit 1 ;;
    esac
    shift
done

PCLFE_HOME=$TARGET_HOME
# 后续调用的 pclfe 子进程要认同一个数据目录（--prefix 可能不是默认值）。
export PCLFE_HOME
mkdir -p "$PCLFE_HOME/bin" "$PCLFE_HOME/lib" "$PCLFE_HOME/packaging" \
         "$PCLFE_HOME/app" "$PCLFE_HOME/jre" "$PCLFE_HOME/logs" \
         "$PCLFE_HOME/cache" "$PCLFE_HOME/state"
PCLFE_APP_DIR="$PCLFE_HOME/app"
PCLFE_PREFIX_DIR="$PCLFE_HOME/prefix"
PCLFE_JRE_DIR="$PCLFE_HOME/jre"
PCLFE_LOG_DIR="$PCLFE_HOME/logs"
PCLFE_CACHE_DIR="$PCLFE_HOME/cache"
PCLFE_STATE_DIR="$PCLFE_HOME/state"
PCLFE_CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/pclfe-linux"
PCLFE_CONFIG="$PCLFE_CONFIG_DIR/config.env"

step "安装到 $PCLFE_HOME"
cp -f "$SRC_DIR/bin/pclfe" "$PCLFE_HOME/bin/pclfe"
cp -f "$SRC_DIR/lib/"*.sh "$PCLFE_HOME/lib/"
[ -f "$SRC_DIR/README.md" ] && cp -f "$SRC_DIR/README.md" "$PCLFE_HOME/README.md"
[ -d "$SRC_DIR/packaging" ] && cp -Rf "$SRC_DIR/packaging/." "$PCLFE_HOME/packaging/"
chmod +x "$PCLFE_HOME/bin/pclfe"
ok "程序文件已复制"

# 版本清单（供 doctor 校验与版本显示）
for _man in "$SRC_DIR/version.json" "$SRC_DIR/../version.json"; do
    if [ -r "$_man" ]; then
        cp -f "$_man" "$PCLFE_HOME/version.json"
        break
    fi
done

# 命令链接（不支持符号链接的文件系统上退化为包装脚本）
mkdir -p "$BIN_DIR"
if ln -sfn "$PCLFE_HOME/bin/pclfe" "$BIN_DIR/pclfe" 2>/dev/null; then
    ok "命令链接：$BIN_DIR/pclfe"
else
    cat > "$BIN_DIR/pclfe" <<SHIM
#!/bin/sh
exec "$PCLFE_HOME/bin/pclfe" "\$@"
SHIM
    chmod +x "$BIN_DIR/pclfe"
    ok "命令包装脚本：$BIN_DIR/pclfe"
fi
case ":${PATH:-}:" in
    *":$BIN_DIR:"*) ;;
    *) warn "$BIN_DIR 不在 PATH 中，请把它加入 PATH 后再使用 pclfe" ;;
esac

# 桌面项与图标
ICON_SRC="$PCLFE_HOME/packaging/icons/pclfe.png"
if [ ! -f "$ICON_SRC" ] && [ -r "$SRC_DIR/../Plain Craft Launcher 2/Images/icon.ico" ]; then
    ICON_SRC="$SRC_DIR/../Plain Craft Launcher 2/Images/icon.ico"
fi

ICON_DEST="$DATA_HOME/icons/hicolor/256x256/apps/pclfe.png"
if [ -f "$ICON_SRC" ]; then
    mkdir -p "$(dirname "$ICON_DEST")"
    case "$ICON_SRC" in
        *.png) cp -f "$ICON_SRC" "$ICON_DEST" ;;
        *.ico)
            if have python3; then
                python3 - "$ICON_SRC" "$ICON_DEST" <<'PY' || warn "图标转换失败，跳过"
import struct, sys
src, dst = sys.argv[1], sys.argv[2]
data = open(src, "rb").read()
count = struct.unpack("<H", data[4:6])[0]
best = None
for i in range(count):
    w, h, _c, _r, _p, bpp, size, off = struct.unpack("<BBBBHHII", data[6 + 16 * i:22 + 16 * i])
    w = w or 256; h = h or 256
    if data[off:off + 8] == b"\x89PNG\r\n\x1a\n" and (best is None or w * h > best[0]):
        best = (w * h, off, size)
if not best or best[2] <= 0:
    sys.exit(1)
_, off, size = best
open(dst, "wb").write(data[off:off + size])
PY
            else
                warn "缺少 python3，无法把 icon.ico 转成 PNG"
            fi
            ;;
    esac
fi

DESKTOP_DIR="$DATA_HOME/applications"
mkdir -p "$DESKTOP_DIR"
cat > "$DESKTOP_DIR/pclfe.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=PCLFE
Name[zh_CN]=Plain Craft Launcher FireWaterDog Edition
Comment=Minecraft launcher (PCLFE Linux Edition, via Wine)
Comment[zh_CN]=Minecraft 启动器（PCLFE Linux 版，基于 Wine）
Exec=$PCLFE_HOME/bin/pclfe run
TryExec=$PCLFE_HOME/bin/pclfe
Icon=pclfe
Terminal=false
Categories=Game;
Keywords=Minecraft;Launcher;PCL;
StartupWMClass=Plain Craft Launcher 2.exe
EOF
ok "桌面项：$DESKTOP_DIR/pclfe.desktop"
if have update-desktop-database; then
    update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
fi

# 初步配置
if [ ! -r "$PCLFE_CONFIG" ]; then
    mkdir -p "$PCLFE_CONFIG_DIR"
    save_config
fi

log ""
if [ "$DO_DEPS" -eq 1 ]; then
    deps_print_report || warn "依赖未装齐，安装完成后请按上面的命令补齐"
fi

if [ -n "$FROM_PATH" ]; then
    step "安装启动器本体"
    "$PCLFE_HOME/bin/pclfe" install --from "$FROM_PATH" || warn "启动器本体安装失败"
fi

if [ "$DO_SETUP" -eq 1 ]; then
    log ""
    if confirm "现在初始化 Wine 前缀并安装 .NET Framework 4.8？（耗时较长，可稍后用 pclfe setup 执行）" y; then
        "$PCLFE_HOME/bin/pclfe" setup || warn "初始化未完成，可修复后重试：pclfe setup"
    else
        dim "已跳过初始化，稍后执行：pclfe setup"
    fi
fi

log ""
ok "安装完成"
log "  数据目录：$PCLFE_HOME"
log "  命令：    $BIN_DIR/pclfe"
log ""
log "下一步："
log "  pclfe doctor    环境自检"
log "  pclfe update    下载启动器"
log "  pclfe run       启动"
