#!/usr/bin/env sh
# 打包 Linux 版发布件：pclfe-linux-<版本>.tar.gz + .sha256
# 产物可直接分发，解压后运行 ./install.sh。

set -u

SRC_DIR=$(cd "$(dirname "$0")" && pwd -P)
# shellcheck source=lib/common.sh
. "$SRC_DIR/lib/common.sh"

OUT_DIR=${1:-$SRC_DIR/dist}
VERSION=$(pclfe_version)
[ "$VERSION" = "unknown" ] && VERSION=$(date '+%Y%m%d')

PKG_NAME="pclfe-linux-$VERSION"
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
    log "SHA256：$_sum"
else
    warn "没有 sha256 工具，未生成校验文件"
fi

rm -rf "$STAGE"
log ""
log "分发方式："
log "  tar xzf $PKG_NAME.tar.gz && cd $PKG_NAME && ./install.sh"
