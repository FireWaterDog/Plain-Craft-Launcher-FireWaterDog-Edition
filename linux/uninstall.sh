#!/usr/bin/env sh
# 卸载 PCLFE Linux Edition。默认保留数据（游戏、账号），--purge 才会删除。

set -u

SRC_DIR=$(cd "$(dirname "$0")" && pwd -P)
# shellcheck source=lib/common.sh
. "$SRC_DIR/lib/common.sh"

TARGET_HOME=${PCLFE_HOME:-${XDG_DATA_HOME:-$HOME/.local/share}/pclfe-linux}
BIN_DIR=${PCLFE_BIN_DIR:-$HOME/.local/bin}
DATA_HOME=${XDG_DATA_HOME:-$HOME/.local/share}
PURGE=0

while [ $# -gt 0 ]; do
    case "$1" in
        --prefix) [ $# -ge 2 ] || die "--prefix 需要参数"; TARGET_HOME=$2; shift ;;
        --prefix=*) TARGET_HOME=${1#--prefix=} ;;
        --purge) PURGE=1 ;;
        --help|-h)
            log "用法：./linux/uninstall.sh [--prefix DIR] [--purge]"
            log "  --purge  同时删除数据目录（游戏文件、Wine 前缀、日志）"
            exit 0
            ;;
        *) die "未知参数：$1" ;;
    esac
    shift
done

step "卸载 PCLFE Linux Edition"

if [ -L "$BIN_DIR/pclfe" ] || [ -f "$BIN_DIR/pclfe" ]; then
    rm -f "$BIN_DIR/pclfe"
    ok "已删除命令链接：$BIN_DIR/pclfe"
fi

if [ -f "$DATA_HOME/applications/pclfe.desktop" ]; then
    rm -f "$DATA_HOME/applications/pclfe.desktop"
    ok "已删除桌面项"
fi

if [ -f "$DATA_HOME/icons/hicolor/256x256/apps/pclfe.png" ]; then
    rm -f "$DATA_HOME/icons/hicolor/256x256/apps/pclfe.png"
    ok "已删除图标"
fi

if [ "$PURGE" -eq 1 ]; then
    if [ -d "$TARGET_HOME" ]; then
        log ""
        warn "即将删除数据目录（包含游戏文件、存档、Wine 前缀）：$TARGET_HOME"
        if confirm "确认删除？此操作不可恢复" n; then
            rm -rf "$TARGET_HOME"
            ok "已删除 $TARGET_HOME"
        else
            warn "已取消删除数据目录"
        fi
    fi
    if [ -f "${XDG_CONFIG_HOME:-$HOME/.config}/pclfe-linux/config.env" ]; then
        rm -rf "${XDG_CONFIG_HOME:-$HOME/.config}/pclfe-linux"
        ok "已删除配置目录"
    fi
else
    log ""
    log "程序文件保留在：$TARGET_HOME"
    log "  如需彻底删除（含游戏与存档）：./linux/uninstall.sh --purge"
fi

ok "卸载完成"
