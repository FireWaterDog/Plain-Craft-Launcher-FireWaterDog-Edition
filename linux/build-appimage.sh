#!/usr/bin/env sh
# 构建 AppImage 单文件应用：pclfe-<版本>-x86_64.AppImage
#
# AppImage 免安装、免 root：双击或 ./pclfe-*.AppImage 即可运行，
# 运行期数据同样写在 ~/.local/share/pclfe-linux。
#
# 用法：
#   sh build-appimage.sh [输出目录]                 只装封装层
#   sh build-appimage.sh [输出目录] --with-exe <exe> 附带启动器本体
#
# 依赖：mksquashfs（squashfs-tools）；appimagetool 会自动下载到缓存目录。

set -u

SRC_DIR=$(cd "$(dirname "$0")" && pwd -P)
# shellcheck source=lib/common.sh
. "$SRC_DIR/lib/common.sh"
# shellcheck source=lib/deps.sh
. "$SRC_DIR/lib/deps.sh"

OUT_DIR=''
BUNDLE_EXE=${PCLFE_BUNDLE_EXE:-}
BUNDLE_DOTNET=''
BUNDLE_WINE=''

while [ $# -gt 0 ]; do
    case "$1" in
        --with-exe) [ $# -ge 2 ] || die "--with-exe 需要参数"; BUNDLE_EXE=$2; shift ;;
        --with-exe=*) BUNDLE_EXE=${1#--with-exe=} ;;
        --with-dotnet) BUNDLE_DOTNET=auto ;;
        --with-dotnet=*) BUNDLE_DOTNET=${1#--with-dotnet=} ;;
        --with-wine) BUNDLE_WINE=auto ;;
        --with-wine=*) BUNDLE_WINE=${1#--with-wine=} ;;
        --help|-h)
            log "用法：sh build-appimage.sh [输出目录] [--with-exe <启动器.exe>] [--with-dotnet[=安装包]] [--with-wine[=tar.xz]]"
            exit 0
            ;;
        -*) die "未知参数：$1" ;;
        *) OUT_DIR=$1 ;;
    esac
    shift
done

OUT_DIR=${OUT_DIR:-$SRC_DIR/dist}
VERSION=${PCLFE_VERSION:-$(pclfe_version)}
[ "$VERSION" = "unknown" ] && VERSION=$(date '+%Y%m%d')

case "$(wine_arch)" in
    x86_64) ARCH=x86_64 ;;
    *) die "AppImage 目前只支持 x86_64（当前：$(wine_arch)）" ;;
esac

APPIMAGETOOL_URL="https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
# 新版 appimagetool 把 runtime 拆成独立文件，且内置下载器在部分网络下会失败；
# 这里自己下载后用 --runtime-file 显式传入。
APPIMAGE_RUNTIME_URL="https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64"
BUILD="$OUT_DIR/.appimage-build"
APPDIR="$BUILD/pclfe.AppDir"

# 找可用的 appimagetool：系统已有就用，否则下载到缓存。
resolve_appimagetool() {
    if have appimagetool; then
        printf 'appimagetool\n'
        return 0
    fi
    _tool="$PCLFE_CACHE_DIR/appimagetool-$ARCH.AppImage"
    # 只看文件是否已存在：下载来源不带可执行位，不能拿 -x 判断
    if [ ! -s "$_tool" ]; then
        deps_require_net
        info "下载 appimagetool（约 15 MB）"
        download "$APPIMAGETOOL_URL" "$_tool" 300 || return 1
    fi
    chmod +x "$_tool" 2>/dev/null || true
    # 没有 FUSE 的环境（容器、WSL1）先解包再执行。
    _extract="$PCLFE_CACHE_DIR/appimagetool-extract"
    if [ ! -x "$_extract/squashfs-root/AppRun" ]; then
        rm -rf "$_extract"
        mkdir -p "$_extract"
        ( cd "$_extract" && "$_tool" --appimage-extract >/dev/null 2>&1 ) || return 1
    fi
    printf '%s\n' "$_extract/squashfs-root/AppRun"
}

step "构建 pclfe-$VERSION-$ARCH.AppImage"

TOOL=$(resolve_appimagetool) || die "无法准备 appimagetool"
dim "appimagetool：$TOOL"

# appimagetool 自带 mksquashfs；没有的话要求系统安装 squashfs-tools。
if ! have mksquashfs; then
    _tool_bin=$(dirname "$TOOL")/usr/bin
    if [ -x "$_tool_bin/mksquashfs" ]; then
        PATH="$_tool_bin:$PATH"
        export PATH
    else
        die "需要 mksquashfs：sudo apt install squashfs-tools"
    fi
fi

# appimagetool 需要 file(1) 判断 ELF 架构
have file || die "需要 file 命令：sudo apt install file"

RUNTIME_FILE="$PCLFE_CACHE_DIR/type2-runtime-$ARCH"
if [ ! -s "$RUNTIME_FILE" ]; then
    deps_require_net
    info "下载 AppImage runtime"
    download "$APPIMAGE_RUNTIME_URL" "$RUNTIME_FILE" 300 || die "无法下载 AppImage runtime"
fi
chmod +x "$RUNTIME_FILE" 2>/dev/null || true

rm -rf "$BUILD"
mkdir -p "$APPDIR/usr/bin" \
         "$APPDIR/usr/lib/pclfe/bin" \
         "$APPDIR/usr/lib/pclfe/lib" \
         "$APPDIR/usr/lib/pclfe/packaging/icons" \
         "$APPDIR/usr/share/applications" \
         "$APPDIR/usr/share/icons/hicolor/256x256/apps"

cp -f "$SRC_DIR/bin/pclfe" "$APPDIR/usr/lib/pclfe/bin/pclfe"
cp -f "$SRC_DIR/lib/"*.sh "$APPDIR/usr/lib/pclfe/lib/"
cp -f "$SRC_DIR/README.md" "$APPDIR/usr/lib/pclfe/README.md"
cp -f "$SRC_DIR/packaging/icons/pclfe.png" "$APPDIR/usr/lib/pclfe/packaging/icons/pclfe.png"
cp -f "$SRC_DIR/packaging/icons/pclfe.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/pclfe.png"
cp -f "$SRC_DIR/packaging/icons/pclfe.png" "$APPDIR/pclfe.png"

for _man in "$SRC_DIR/version.json" "$SRC_DIR/../version.json"; do
    if [ -r "$_man" ]; then cp -f "$_man" "$APPDIR/usr/lib/pclfe/version.json"; break; fi
done

if [ -n "$BUNDLE_EXE" ]; then
    [ -f "$BUNDLE_EXE" ] || die "启动器本体不存在：$BUNDLE_EXE"
    mkdir -p "$APPDIR/usr/lib/pclfe/app"
    cp -f "$BUNDLE_EXE" "$APPDIR/usr/lib/pclfe/app/$PCLFE_EXE_NAME"
    _sum=$(sha256_of "$APPDIR/usr/lib/pclfe/app/$PCLFE_EXE_NAME" || true)
    [ -n "$_sum" ] && printf '%s  %s\n' "$_sum" "$PCLFE_EXE_NAME" \
        > "$APPDIR/usr/lib/pclfe/app/exe.sha256"
    dim "内置启动器：$(basename "$BUNDLE_EXE")（$(human_size "$APPDIR/usr/lib/pclfe/app/$PCLFE_EXE_NAME")）"
fi

if [ -n "$BUNDLE_DOTNET" ]; then
    if [ "$BUNDLE_DOTNET" = "auto" ]; then
        BUNDLE_DOTNET=$(dotnet_installer_fetch) || die "下载 .NET Framework 4.8 安装包失败"
    fi
    [ -f "$BUNDLE_DOTNET" ] || die ".NET 安装包不存在：$BUNDLE_DOTNET"
    mkdir -p "$APPDIR/usr/lib/pclfe/dotnet"
    cp -f "$BUNDLE_DOTNET" "$APPDIR/usr/lib/pclfe/dotnet/$PCLFE_DOTNET_FILE"
    chmod 644 "$APPDIR/usr/lib/pclfe/dotnet/$PCLFE_DOTNET_FILE"
    dim "内置 .NET 4.8：$(human_size "$APPDIR/usr/lib/pclfe/dotnet/$PCLFE_DOTNET_FILE")"
fi

ln -sfn ../lib/pclfe/bin/pclfe "$APPDIR/usr/bin/pclfe"

# AppRun：容器入口
cat > "$APPDIR/AppRun" <<'APPRUN'
#!/bin/sh
HERE=$(dirname "$(readlink -f "$0")")
export PCLFE_APPIMAGE=1
# AppImage 内程序目录只读，首次运行会把启动器复制到用户数据目录
exec "$HERE/usr/bin/pclfe" "$@"
APPRUN
chmod 755 "$APPDIR/AppRun"

# 桌面项（AppImage 要求位于根目录，且 Exec 与可执行文件同名）
cat > "$APPDIR/pclfe.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=PCLFE
Name[zh_CN]=Plain Craft Launcher FireWaterDog Edition
GenericName=Minecraft Launcher
Comment=Minecraft launcher (PCLFE Linux Edition, via Wine)
Comment[zh_CN]=Minecraft 启动器（PCLFE Linux 版，基于 Wine）
Exec=pclfe
Icon=pclfe
Terminal=false
Categories=Game;
Keywords=Minecraft;Launcher;PCL;
StartupWMClass=Plain Craft Launcher 2.exe
DESKTOP
cp -f "$APPDIR/pclfe.desktop" "$APPDIR/usr/share/applications/pclfe.desktop"

chmod 755 "$APPDIR/usr/lib/pclfe/bin/pclfe" "$APPDIR/usr/lib/pclfe/lib/"*.sh

if [ -n "$BUNDLE_WINE" ]; then
    if [ "$BUNDLE_WINE" = "auto" ]; then
        BUNDLE_WINE=$(wine_vendor_fetch) || die "下载便携版 Wine 失败"
    fi
    [ -f "$BUNDLE_WINE" ] || die "Wine 压缩包不存在：$BUNDLE_WINE"
    mkdir -p "$APPDIR/usr/lib/pclfe/vendor"
    cp -f "$BUNDLE_WINE" "$APPDIR/usr/lib/pclfe/vendor/$(basename "$BUNDLE_WINE")"
    chmod 644 "$APPDIR/usr/lib/pclfe/vendor/$(basename "$BUNDLE_WINE")"
    dim "内置 Wine：$(human_size "$APPDIR/usr/lib/pclfe/vendor/$(basename "$BUNDLE_WINE")")"
fi

NAME_SUFFIX=''
[ -n "$BUNDLE_DOTNET" ] && NAME_SUFFIX="${NAME_SUFFIX}-net"
[ -n "$BUNDLE_WINE" ] && NAME_SUFFIX="${NAME_SUFFIX}-wine"
OUT_FILE="$OUT_DIR/pclfe-$VERSION-$ARCH$NAME_SUFFIX.AppImage"
rm -f "$OUT_FILE" "$OUT_FILE.sha256"

# appimagetool 结束后会留下 AppDir 里的临时文件，构建完全部清掉
ARCH=$ARCH "$TOOL" --no-appstream --runtime-file "$RUNTIME_FILE" "$APPDIR" "$OUT_FILE" 2>&1 | tail -5 || {
    rm -rf "$BUILD"
    die "appimagetool 构建失败"
}

[ -f "$OUT_FILE" ] || die "未生成 AppImage"
chmod 755 "$OUT_FILE"

if _sum=$(sha256_of "$OUT_FILE"); then
    printf '%s  %s\n' "$_sum" "$(basename "$OUT_FILE")" > "$OUT_FILE.sha256"
fi

ok "产物：$OUT_FILE"
log "  大小：$(human_size "$OUT_FILE")"
[ -n "${_sum:-}" ] && log "  SHA256：$_sum"
log ""
log "运行："
log "  chmod +x $(basename "$OUT_FILE") && ./$(basename "$OUT_FILE")"
rm -rf "$BUILD"
