#!/usr/bin/env sh
# 启动 PCLFE：解析程序位置、组装 Wine 环境、转发输出到日志。

runtime_exe() {
    for _cand in "$PCLFE_APP_DIR/$PCLFE_EXE_NAME" "$PCLFE_APP_DIR/$PCLFE_RELEASE_EXE_NAME"; do
        [ -f "$_cand" ] && { printf '%s\n' "$_cand"; return 0; }
    done
    # 兜底：应用目录下任意 exe
    for _cand in "$PCLFE_APP_DIR"/*.exe; do
        [ -f "$_cand" ] && { printf '%s\n' "$_cand"; return 0; }
    done
    return 1
}

# 组装启动所需的环境变量。
runtime_env() {
    export WINEPREFIX="$PCLFE_PREFIX_DIR"
    export WINEARCH=win64
    export WINEDEBUG="$PCLFE_WINEDEBUG"

    # Wine 在非 UTF-8 locale 下会把中文路径变成乱码，进而找不到 .minecraft。
    case "${LC_ALL:-${LANG:-}}" in
        *UTF-8*|*utf8*|*UTF8*) ;;
        *) export LANG=C.UTF-8 LC_ALL=C.UTF-8 ;;
    esac

    # 音频：Wine 下 PulseAudio 缓冲区过小会让游戏声音爆音、卡顿。
    export PULSE_LATENCY_MSEC="${PULSE_LATENCY_MSEC:-60}"

    # 同步原语：内核支持时显著降低 CPU 占用。
    if [ "$PCLFE_ESYNC" = "1" ]; then
        export WINEESYNC=1
        export WINEFSYNC=1
    fi

    # Java 桥接：让 PCL 的 Java 扫描能通过 JAVA_HOME 找到 Windows 版 JRE。
    # 注意宿主机的 JAVA_HOME 通常是 Linux 路径（如 /usr/lib/jvm/...），Wine 里的程序
    # 无法执行它；只有 Windows 形式（Z:\... 或 C:\...）才保留用户设置。
    _ours=$(java_export_home 2>/dev/null || true)
    case "${JAVA_HOME:-}" in
        [A-Za-z]:[\\/]*)
            # 用户已指定 Windows 路径，尊重它
            ;;
        *)
            if [ -n "$_ours" ]; then
                export JAVA_HOME="$_ours"
            fi
            ;;
    esac

    case "${JAVA_HOME:-}" in
        [A-Za-z]:[\\/]*) ;;
        '') ;;
        *)
            warn "JAVA_HOME 指向 Linux 路径，Wine 内的启动器无法执行；可执行 pclfe java install 21"
            ;;
    esac

    # WebView2：使用外部固定版本运行时（若用户指定；允许写 Linux 路径，这里转成 Z: 形式）。
    if [ -n "${PCLFE_WEBVIEW2_FOLDER:-}" ]; then
        case "$PCLFE_WEBVIEW2_FOLDER" in
            /*)
                _wv2=$(win_path "$PCLFE_WEBVIEW2_FOLDER" 2>/dev/null || true)
                export WEBVIEW2_BROWSER_EXECUTABLE_FOLDER="${_wv2:-$PCLFE_WEBVIEW2_FOLDER}"
                ;;
            *)
                export WEBVIEW2_BROWSER_EXECUTABLE_FOLDER="$PCLFE_WEBVIEW2_FOLDER"
                ;;
        esac
    fi

    # DXVK：把 D3D 调用转到 Vulkan，显著改善部分显卡下的画面与性能。
    # PCLFE_RENDERER 可取 auto（跟随 PCLFE_DXVK）/ vulkan（强制 DXVK）/ gl（强制 WineD3D）。
    if dxvk_enabled; then
        export WINEDLLOVERRIDES="d3d9,d3d10core,d3d11,dxgi=n"
        if [ -n "${DXVK_HUD:-}" ]; then
            export DXVK_HUD
        fi
    fi

    # 让 Wine 的浏览器调用走宿主机的 xdg-open（登录页、帮助链接用得到）。
    export BROWSER="${BROWSER:-xdg-open}"
}

# 是否启用 DXVK：显式渲染器设置优先，其次看 PCLFE_DXVK。
dxvk_enabled() {
    case "${PCLFE_RENDERER:-auto}" in
        vulkan|dxvk) return 0 ;;
        gl|opengl|wined3d) return 1 ;;
        *) [ "${PCLFE_DXVK:-0}" = "1" ] ;;
    esac
}

runtime_preflight() {
    _exe=$(runtime_exe) || {
        err "未找到启动器程序（$PCLFE_EXE_NAME）"
        dim "请先执行：pclfe update       # 从发布页下载"
        dim "或：     pclfe install --from /path/to/bin.zip"
        return 1
    }
    [ -x "$_exe" ] || chmod +x "$_exe" 2>/dev/null || true

    if ! prefix_exists; then
        err "Wine 前缀尚未初始化"
        dim "请先执行：pclfe setup"
        return 1
    fi
    if ! dotnet48_installed; then
        warn ".NET Framework 4.8 未检测到，启动可能失败"
        dim "可执行：pclfe setup"
    fi
    return 0
}

# runtime_run [传给启动器的参数...]
runtime_run() {
    runtime_preflight || return 1
    _wine=$(wine_bin) || die "未找到 wine，请先安装"

    runtime_env
    _exe=$(runtime_exe)
    mkdir -p "$PCLFE_LOG_DIR"
    _log="$PCLFE_LOG_DIR/run-$(timestamp).log"
    _status_file="$PCLFE_CACHE_DIR/.run-status"

    info "启动器：$_exe"
    dim "Wine 前缀：$PCLFE_PREFIX_DIR"
    dim "运行日志：$_log"
    [ -n "${JAVA_HOME:-}" ] && dim "JAVA_HOME：$JAVA_HOME"
    log ""

    # 在程序目录内启动，保证 PCL 的便携数据（PCL 文件夹）落在应用目录。
    cd "$PCLFE_APP_DIR" || return 1
    {
        "$_wine" "$_exe" "$@" 2>&1
        printf '%s' "$?" > "$_status_file"
    } | tee -a "$_log"

    _code=$(cat "$_status_file" 2>/dev/null || echo 1)
    rm -f "$_status_file"

    if [ "$_code" = "0" ]; then
        ok "启动器已退出（日志：$_log）"
    else
        err "启动器异常退出，退出码 $_code"
        dim "查看日志最后 40 行："
        tail -n 40 "$_log" | sed 's/^/    /'
        dim "更多排查：pclfe doctor"
    fi
    return "$_code"
}
