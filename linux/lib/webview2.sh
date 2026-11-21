#!/usr/bin/env sh
# WebView2 运行时的安装与检测。
#
# 作用：PCLFE 的微软正版登录使用「授权码流 + 内置浏览器（WebView2）」，
#       内置浏览器起不来时，正版登录页无法显示，只能退回离线登录。
#
# 两种落地方式：
#   1. 在前缀里安装 Evergreen 运行时（本文件的默认做法）；
#   2. 已经有一份解压好的固定版本运行时，把 PCLFE_WEBVIEW2_FOLDER 指向它，
#      启动时通过 WEBVIEW2_BROWSER_EXECUTABLE_FOLDER 传给程序。

WEBVIEW2_STANDALONE_URL="https://go.microsoft.com/fwlink/?linkid=2124701"
WEBVIEW2_BOOTSTRAPPER_URL="https://go.microsoft.com/fwlink/?linkid=2124703"

webview2_installed() { webview2_prefix_check; }

webview2_version() {
    _exe=$(ls "$PCLFE_PREFIX_DIR/drive_c/Program Files (x86)/Microsoft/EdgeWebView/Application"/*/msedgewebview2.exe 2>/dev/null | head -n 1)
    [ -n "$_exe" ] || return 1
    basename "$(dirname "$_exe")"
}

webview2_install() {
    prefix_exists || die "请先执行 'pclfe setup' 创建 Wine 前缀"
    deps_require_net

    if webview2_installed; then
        ok "WebView2 运行时已安装（$(webview2_version)）"
        return 0
    fi

    step "安装 WebView2 运行时（用于微软正版登录）"
    dim "说明：WebView2 依赖 Edge 的安装器，在 Wine 下成功率不高；失败不影响启动与离线登录。"

    _dir="$PCLFE_CACHE_DIR/webview2"
    mkdir -p "$_dir"

    # 先试离线安装包（体量大但不需要在 Wine 内联网），失败再试在线引导器。
    for _pair in "standalone:$WEBVIEW2_STANDALONE_URL" "bootstrapper:$WEBVIEW2_BOOTSTRAPPER_URL"; do
        _kind=${_pair%%:*}
        _url=${_pair#*:}
        _file="$_dir/WebView2Setup-$_kind.exe"
        info "下载 $_kind 安装包"
        if ! download "$_url" "$_file" 900; then
            warn "下载 $_kind 安装包失败，跳过"
            continue
        fi
        info "运行 $_kind 安装包（可能需要几分钟）"
        _wine=$(wine_bin)
        WINEPREFIX="$PCLFE_PREFIX_DIR" WINEDEBUG=-all "$_wine" "$_file" /silent /install >/dev/null 2>&1 || true
        if webview2_installed; then
            ok "WebView2 运行时安装成功（$(webview2_version)）"
            return 0
        fi
        warn "$_kind 安装包未能完成安装"
    done

    warn "WebView2 安装失败"
    log ""
    log "可选方案："
    log "  1. 换用 wine-staging 新版本后重试：pclfe webview2 install"
    log "  2. 手动下载「WebView2 固定版本」运行时解压到任意目录，然后："
    log "     export PCLFE_WEBVIEW2_FOLDER='Z:\\path\\to\\msedgewebview2'"
    log "  3. 先用离线登录；或在 Windows 上登录同一账号后，把 PCL 数据目录整体复制过来"
    log "     （PCL 为便携式启动器，配置跟随程序目录，未在 Wine 下逐一验证）"
    return 1
}

webview2_status() {
    if webview2_installed; then
        log "WebView2 运行时：已安装（$(webview2_version)）"
    elif [ -n "${PCLFE_WEBVIEW2_FOLDER:-}" ]; then
        log "WebView2 运行时：使用外部目录 $PCLFE_WEBVIEW2_FOLDER"
    else
        log "WebView2 运行时：未安装（微软正版登录不可用，离线登录不受影响）"
    fi
}
