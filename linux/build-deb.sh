#!/usr/bin/env sh
# 构建 Debian 系安装包：pclfe_<版本>_all.deb
#
# 用法：
#   sh build-deb.sh [输出目录]                 只装封装层（首次运行需 pclfe update）
#   sh build-deb.sh [输出目录] --with-exe <exe> 附带启动器本体（装完即可启动）
#
# 安装布局：
#   /usr/bin/pclfe                                 命令入口（符号链接）
#   /usr/lib/pclfe/bin|lib|packaging               程序本体
#   /usr/lib/pclfe/app/<exe>                       可选的启动器本体
#   /usr/share/applications/pclfe.desktop          应用菜单项
#   /usr/share/icons/hicolor/256x256/apps/pclfe.png
#   /usr/share/doc/pclfe/                          文档与版权
# 运行期数据仍写用户目录：~/.local/share/pclfe-linux

set -u

SRC_DIR=$(cd "$(dirname "$0")" && pwd -P)
# shellcheck source=lib/common.sh
. "$SRC_DIR/lib/common.sh"

OUT_DIR=''
BUNDLE_EXE=${PCLFE_BUNDLE_EXE:-}

while [ $# -gt 0 ]; do
    case "$1" in
        --with-exe) [ $# -ge 2 ] || die "--with-exe 需要参数"; BUNDLE_EXE=$2; shift ;;
        --with-exe=*) BUNDLE_EXE=${1#--with-exe=} ;;
        --help|-h)
            log "用法：sh build-deb.sh [输出目录] [--with-exe <启动器.exe>]"
            exit 0
            ;;
        -*) die "未知参数：$1" ;;
        *) OUT_DIR=$1 ;;
    esac
    shift
done

OUT_DIR=${OUT_DIR:-$SRC_DIR/dist}
VERSION=${PCLFE_VERSION:-$(pclfe_version)}
[ "$VERSION" = "unknown" ] && VERSION=0.0.0

PKG=pclfe
DEB_NAME="${PKG}_${VERSION}_all.deb"
BUILD="$OUT_DIR/.deb-build"
ROOT="$BUILD/root"

have dpkg-deb || die "需要 dpkg-deb（Debian/Ubuntu 自带）"

step "构建 $DEB_NAME"
rm -rf "$BUILD"
mkdir -p "$ROOT/DEBIAN" \
         "$ROOT/usr/bin" \
         "$ROOT/usr/lib/pclfe/bin" \
         "$ROOT/usr/lib/pclfe/lib" \
         "$ROOT/usr/lib/pclfe/packaging/icons" \
         "$ROOT/usr/share/applications" \
         "$ROOT/usr/share/icons/hicolor/256x256/apps" \
         "$ROOT/usr/share/doc/pclfe"

cp -f "$SRC_DIR/bin/pclfe" "$ROOT/usr/lib/pclfe/bin/pclfe"
cp -f "$SRC_DIR/lib/"*.sh "$ROOT/usr/lib/pclfe/lib/"
cp -f "$SRC_DIR/README.md" "$ROOT/usr/lib/pclfe/README.md"
cp -f "$SRC_DIR/README.md" "$ROOT/usr/share/doc/pclfe/README.md"
cp -f "$SRC_DIR/packaging/icons/pclfe.png" "$ROOT/usr/lib/pclfe/packaging/icons/pclfe.png"
cp -f "$SRC_DIR/packaging/icons/pclfe.png" "$ROOT/usr/share/icons/hicolor/256x256/apps/pclfe.png"

# 版本清单：让 pclfe version / doctor 在系统级安装下也能读到版本
for _man in "$SRC_DIR/version.json" "$SRC_DIR/../version.json"; do
    if [ -r "$_man" ]; then cp -f "$_man" "$ROOT/usr/lib/pclfe/version.json"; break; fi
done

chmod 755 "$ROOT/usr/lib/pclfe/bin/pclfe"
chmod 755 "$ROOT/usr/lib/pclfe/lib/"*.sh
chmod 644 "$ROOT/usr/lib/pclfe/README.md" "$ROOT/usr/share/doc/pclfe/README.md" \
          "$ROOT/usr/lib/pclfe/packaging/icons/pclfe.png" \
          "$ROOT/usr/share/icons/hicolor/256x256/apps/pclfe.png"
[ -f "$ROOT/usr/lib/pclfe/version.json" ] && chmod 644 "$ROOT/usr/lib/pclfe/version.json"

# 命令入口：指向程序目录
ln -sfn ../lib/pclfe/bin/pclfe "$ROOT/usr/bin/pclfe"

# 桌面项
cat > "$ROOT/usr/share/applications/pclfe.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=PCLFE
Name[zh_CN]=Plain Craft Launcher FireWaterDog Edition
GenericName=Minecraft Launcher
Comment=Minecraft launcher (PCLFE Linux Edition, via Wine)
Comment[zh_CN]=Minecraft 启动器（PCLFE Linux 版，基于 Wine）
Exec=/usr/bin/pclfe run
TryExec=/usr/bin/pclfe
Icon=pclfe
Terminal=false
Categories=Game;
Keywords=Minecraft;Launcher;PCL;
StartupWMClass=Plain Craft Launcher 2.exe
DESKTOP
chmod 644 "$ROOT/usr/share/applications/pclfe.desktop"

# 离线包：把启动器本体放进 /usr/lib/pclfe/app，首次运行会复制到用户目录
if [ -n "$BUNDLE_EXE" ]; then
    [ -f "$BUNDLE_EXE" ] || die "启动器本体不存在：$BUNDLE_EXE"
    mkdir -p "$ROOT/usr/lib/pclfe/app"
    cp -f "$BUNDLE_EXE" "$ROOT/usr/lib/pclfe/app/$PCLFE_EXE_NAME"
    chmod 644 "$ROOT/usr/lib/pclfe/app/$PCLFE_EXE_NAME"
    _exe_sum=$(sha256_of "$ROOT/usr/lib/pclfe/app/$PCLFE_EXE_NAME" || true)
    [ -n "$_exe_sum" ] && printf '%s  %s\n' "$_exe_sum" "$PCLFE_EXE_NAME" \
        > "$ROOT/usr/lib/pclfe/app/exe.sha256"
    dim "内置启动器：$(basename "$BUNDLE_EXE")（$(human_size "$ROOT/usr/lib/pclfe/app/$PCLFE_EXE_NAME")）"
fi

# 版权
cat > "$ROOT/usr/share/doc/pclfe/copyright" <<EOF
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: Plain Craft Launcher FireWaterDog Edition
Source: https://github.com/FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition

Files: *
Copyright: FireWaterDog
License: see /usr/lib/pclfe/README.md and the upstream LICENCE file
 PCLFE 为 PCL2 的二次开发版本，PCL 版权归原作者（龙腾猫跃）所有。
EOF
chmod 644 "$ROOT/usr/share/doc/pclfe/copyright"

# 安装后刷新桌面与图标缓存（失败不影响安装）
cat > "$ROOT/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if [ "$1" = "configure" ]; then
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
    command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
fi
exit 0
POSTINST
chmod 755 "$ROOT/DEBIAN/postinst"

cat > "$ROOT/DEBIAN/postrm" <<'POSTRM'
#!/bin/sh
set -e
if [ "$1" = "remove" ] || [ "$1" = "purge" ]; then
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
fi
exit 0
POSTRM
chmod 755 "$ROOT/DEBIAN/postrm"

# 控制信息
INSTALLED_SIZE=$(du -sk "$ROOT/usr" | awk '{print $1}')
# 只有轻量且各发行版都稳定存在的放进 Depends；
# wine / winetricks 放进 Recommends —— apt 默认会装，但发行版改了包名也不至于装不上。
DEPS='ca-certificates, curl | wget'
RECS='wine | wine64, winetricks, cabextract, unzip, fonts-noto-cjk | fonts-wqy-zenhei, mesa-utils, zenity | kdialog'
cat > "$ROOT/DEBIAN/control" <<EOF
Package: $PKG
Version: $VERSION
Section: games
Priority: optional
Architecture: all
Installed-Size: $INSTALLED_SIZE
Depends: $DEPS
Recommends: $RECS
Maintainer: FireWaterDog <firewaterdog@users.noreply.github.com>
Homepage: https://github.com/FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition
Description: PCLFE Linux Edition - Minecraft launcher (Wine wrapper)
 Plain Craft Launcher FireWaterDog Edition 的 Linux 封装版，通过 Wine 运行原版
 启动器（.NET Framework 4.8 + WPF），界面与功能同 Windows 版。
 .
 提供 pclfe 命令：setup（初始化 Wine 前缀与 .NET 4.8）、update（下载启动器）、
 run（启动）、doctor（环境自检）、java（安装 Windows 版 JRE）等。
 .
 运行期数据保存在 ~/.local/share/pclfe-linux，卸载本包不会删除游戏与存档。
EOF
chmod 644 "$ROOT/DEBIAN/control"

DEB="$OUT_DIR/$DEB_NAME"
rm -f "$DEB"
# 压缩格式用 xz：dpkg 1.15.6+ 都能读，避免新版 dpkg 默认的 zstd 在老系统
# （Debian 11、Ubuntu 20.04、Deepin/UOS 等）上报
# "unknown compression for member 'control.tar.zst'"。
DEB_COMPRESSION=${PCLFE_DEB_COMPRESSION:-xz}
dpkg-deb --root-owner-group -Z"$DEB_COMPRESSION" --build "$ROOT" "$DEB" >/dev/null \
    || die "dpkg-deb 构建失败"

if _sum=$(sha256_of "$DEB"); then
    printf '%s  %s\n' "$_sum" "$DEB_NAME" > "$DEB.sha256"
fi

ok "产物：$DEB"
log "  大小：$(human_size "$DEB")"
log "  压缩：$DEB_COMPRESSION（兼容老版本 dpkg）"
[ -n "${_sum:-}" ] && log "  SHA256：$_sum"
log ""
log "安装："
log "  sudo apt install ./$DEB_NAME      # 自动补依赖"
log "  sudo dpkg -i ./$DEB_NAME          # 或直接 dpkg"
log "运行：应用菜单里的 PCLFE，或终端 pclfe run"
rm -rf "$BUILD"
