#!/usr/bin/env sh
# 打包 Linux 版发布件：pclfe-linux-<版本>[-offline].tar.gz + .sha256
#
# 用法：
#   sh build-release.sh [输出目录]                  只含封装层（体积小，安装后需 pclfe update）
#   sh build-release.sh [输出目录] --with-exe <exe> 附带启动器本体（离线包，装完即可启动）
#   也可用环境变量 PCLFE_BUNDLE_EXE 指定 exe。
#
# 产物可直接分发，解压后运行 ./install.sh。

set -u

SRC_DIR=$(cd "$(dirname "$0")" && pwd -P)
# shellcheck source=lib/common.sh
. "$SRC_DIR/lib/common.sh"

OUT_DIR=''
BUNDLE_EXE=${PCLFE_BUNDLE_EXE:-}
BUNDLE_DOTNET=''
BUNDLE_WINE=''

while [ $# -gt 0 ]; do
    case "$1" in
        --with-exe)
            [ $# -ge 2 ] || die "--with-exe 需要参数"
            BUNDLE_EXE=$2
            shift
            ;;
        --with-exe=*) BUNDLE_EXE=${1#--with-exe=} ;;
        --with-dotnet) BUNDLE_DOTNET=auto ;;
        --with-dotnet=*) BUNDLE_DOTNET=${1#--with-dotnet=} ;;
        --with-wine) BUNDLE_WINE=auto ;;
        --with-wine=*) BUNDLE_WINE=${1#--with-wine=} ;;
        --help|-h)
            log "用法：sh build-release.sh [输出目录] [--with-exe <启动器.exe>] [--with-dotnet[=安装包]] [--with-wine[=tar.xz]]"
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

SUFFIX=''
[ -n "$BUNDLE_EXE" ] && SUFFIX="${SUFFIX}-offline"
[ -n "$BUNDLE_DOTNET" ] && SUFFIX="${SUFFIX}-net"
[ -n "$BUNDLE_WINE" ] && SUFFIX="${SUFFIX}-wine"
PKG_NAME="pclfe-linux-$VERSION$SUFFIX"
STAGE="$OUT_DIR/$PKG_NAME"

step "打包 $PKG_NAME"
rm -rf "$STAGE"
mkdir -p "$STAGE/bin" "$STAGE/lib" "$STAGE/packaging/icons"

cp -f "$SRC_DIR/bin/pclfe" "$STAGE/bin/pclfe"
cp -f "$SRC_DIR/lib/"*.sh "$STAGE/lib/"
cp -f "$SRC_DIR/install.sh" "$STAGE/install.sh"
cp -f "$SRC_DIR/uninstall.sh" "$STAGE/uninstall.sh"
cp -f "$SRC_DIR/README.md" "$STAGE/README.md"
cp -f "$SRC_DIR/packaging/icons/pclfe.png" "$STAGE/packaging/icons/pclfe.png"

for _man in "$SRC_DIR/version.json" "$SRC_DIR/../version.json"; do
    if [ -r "$_man" ]; then cp -f "$_man" "$STAGE/version.json"; break; fi
done

# 离线包：把启动器本体放进 app/，install.sh 会自动安装它。
if [ -n "$BUNDLE_EXE" ]; then
    [ -f "$BUNDLE_EXE" ] || die "启动器本体不存在：$BUNDLE_EXE"
    mkdir -p "$STAGE/app"
    cp -f "$BUNDLE_EXE" "$STAGE/app/$PCLFE_EXE_NAME"
    _exe_sum=$(sha256_of "$STAGE/app/$PCLFE_EXE_NAME" || true)
    dim "内置启动器：$(basename "$BUNDLE_EXE")（$(human_size "$STAGE/app/$PCLFE_EXE_NAME")）"
    if [ -n "$_exe_sum" ]; then
        printf '%s  app/%s\n' "$_exe_sum" "$PCLFE_EXE_NAME" > "$STAGE/app/exe.sha256"
        # 与版本清单比对，明确告诉使用者这个 exe 是否就是当前发布版
        _want=$(json_str "$STAGE/version.json" sha256 2>/dev/null || true)
        if [ -n "$_want" ] && [ "$_want" != "$_exe_sum" ]; then
            warn "内置 exe 与 version.json 记录的 SHA256 不一致（本地构建或版本不符）"
        fi
    fi
fi

# 离线包：把 .NET 4.8 安装包放进 dotnet/，pclfe setup 会自动使用
if [ -n "$BUNDLE_DOTNET" ]; then
    if [ "$BUNDLE_DOTNET" = "auto" ]; then
        BUNDLE_DOTNET=$(dotnet_installer_fetch) || die "下载 .NET Framework 4.8 安装包失败"
    fi
    [ -f "$BUNDLE_DOTNET" ] || die ".NET 安装包不存在：$BUNDLE_DOTNET"
    mkdir -p "$STAGE/dotnet"
    cp -f "$BUNDLE_DOTNET" "$STAGE/dotnet/$PCLFE_DOTNET_FILE"
    dim "内置 .NET 4.8：$(human_size "$STAGE/dotnet/$PCLFE_DOTNET_FILE")"
fi

# 便携版 Wine（压缩包形式随包分发，首跑自动解压）
if [ -n "$BUNDLE_WINE" ]; then
    if [ "$BUNDLE_WINE" = "auto" ]; then
        BUNDLE_WINE=$(wine_vendor_fetch) || die "下载便携版 Wine 失败"
    fi
    [ -f "$BUNDLE_WINE" ] || die "Wine 压缩包不存在：$BUNDLE_WINE"
    mkdir -p "$STAGE/vendor"
    cp -f "$BUNDLE_WINE" "$STAGE/vendor/$(basename "$BUNDLE_WINE")"
    dim "内置 Wine：$(human_size "$STAGE/vendor/$(basename "$BUNDLE_WINE")")"
fi

# 统一权限与行尾（防止在 Windows 上打包带了 CRLF）。
find "$STAGE" -type f -name '*.sh' -exec chmod +x {} +
chmod +x "$STAGE/bin/pclfe" "$STAGE/install.sh" "$STAGE/uninstall.sh"
find "$STAGE" -type f \( -name '*.sh' -o -name 'pclfe' \) -exec sed -i 's/\r$//' {} + 2>/dev/null || true

TARBALL="$OUT_DIR/$PKG_NAME.tar.gz"
rm -f "$TARBALL" "$TARBALL.sha256"
( cd "$OUT_DIR" && tar czf "$PKG_NAME.tar.gz" "$PKG_NAME" ) || die "打包失败"

if _sum=$(sha256_of "$TARBALL"); then
    printf '%s  %s\n' "$_sum" "$PKG_NAME.tar.gz" > "$TARBALL.sha256"
    ok "产物：$TARBALL"
    log "  大小：$(human_size "$TARBALL")"
    log "  SHA256：$_sum"
else
    warn "没有 sha256 工具，未生成校验文件"
fi

rm -rf "$STAGE"
log ""
log "分发方式："
log "  tar xzf $PKG_NAME.tar.gz && cd $PKG_NAME && ./install.sh"
[ -n "$BUNDLE_EXE" ] && log "  已内置启动器，安装后无需 pclfe update"
[ -n "$BUNDLE_DOTNET" ] && log "  已内置 .NET 4.8 安装包，setup 时不再联网下载"
[ -n "$BUNDLE_WINE" ] && log "  已内置便携 Wine，首次 setup 自动解压，无需系统安装 Wine"
exit 0
