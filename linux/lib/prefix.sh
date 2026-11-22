#!/usr/bin/env sh
# Wine 前缀（prefix）的创建、组件安装与注册表微调。
# PCLFE 本体是 .NET Framework 4.8 + WPF 程序，因此前缀里必须有 dotnet48。

# winetricks 组件：win10 设定 Windows 版本，dotnet48 是运行前提，
# corefonts/gdiplus 保证界面字体与图标正常，vcrun2019 供部分原生组件使用。
PCLFE_WINETRICKS_VERBS="${PCLFE_WINETRICKS_VERBS:-win10 corefonts gdiplus vcrun2019 dotnet48}"
PCLFE_COMPONENTS_STAMP="$PCLFE_STATE_DIR/components.stamp"

prefix_exists() { [ -d "$PCLFE_PREFIX_DIR/drive_c" ]; }

prefix_create() {
    if prefix_exists; then
        dim "Wine 前缀已存在：$PCLFE_PREFIX_DIR"
        return 0
    fi
    mkdir -p "$PCLFE_PREFIX_DIR"
    step "创建 64 位 Wine 前缀"
    _wine=$(wine_bin) || die "未找到 wine，请先安装"
    WINEPREFIX="$PCLFE_PREFIX_DIR" WINEARCH=win64 WINEDEBUG=-all "$_wine" wineboot -u >/dev/null 2>&1 || {
        err "创建 Wine 前缀失败；可用 'pclfe prefix reset' 清理后重试"
        return 1
    }
    # 首次 wineboot 会异步拉起服务进程，稍等它落盘。
    sleep 2
    prefix_exists || { err "Wine 前缀创建不完整：$PCLFE_PREFIX_DIR"; return 1; }
    ok "Wine 前缀已创建"
}

# .NET Framework 4.8 是否已装好。Release >= 528040 表示 4.8。
dotnet48_installed() {
    prefix_exists || return 1
    _wine=$(wine_bin) || return 1
    _out=$(WINEPREFIX="$PCLFE_PREFIX_DIR" WINEDEBUG=-all "$_wine" reg query \
        'HKLM\Software\Microsoft\NET Framework Setup\NDP\v4\Full' /v Release 2>/dev/null || true)
    _rel=$(printf '%s' "$_out" | tr -d '\r' | sed -n 's/.*Release[[:space:]]*REG_DWORD[[:space:]]*0x\([0-9a-fA-F]*\).*/\1/p' | head -n 1)
    [ -n "$_rel" ] || return 1
    [ "$((0x$_rel))" -ge 528040 ] 2>/dev/null
}

webview2_prefix_check() {
    # WebView2 运行时通常装在 Program Files (x86) 下，版本号目录不固定。
    ls "$PCLFE_PREFIX_DIR/drive_c/Program Files (x86)/Microsoft/EdgeWebView/Application"/*/msedgewebview2.exe \
        >/dev/null 2>&1
}

prefix_components_ready() {
    [ -r "$PCLFE_COMPONENTS_STAMP" ] && dotnet48_installed
}

# dotnet48 的安装器是 32 位程序；只有 64 位 Wine 时基本装不上。
wine32_hint() {
    case "$(uname -m)" in
        x86_64) ;;
        *) return 0 ;;
    esac
    for _d in /usr/lib/wine/i386-unix /usr/lib/i386-linux-gnu/wine /usr/lib32/wine; do
        [ -d "$_d" ] && return 0
    done
    warn "未检测到 32 位 Wine；.NET 4.8 的安装器是 32 位程序，很可能失败"
    dim "Debian/Ubuntu：sudo dpkg --add-architecture i386 && sudo apt update && sudo apt install -y wine32:i386"
    dim "Fedora：sudo dnf install wine-core.i686"
    return 1
}

prefix_install_components() {
    prefix_create || return 1

    if prefix_components_ready; then
        ok ".NET Framework 4.8 等组件已就绪"
        return 0
    fi

    have winetricks || die "未找到 winetricks，无法安装 .NET Framework 4.8"
    wine32_hint || true

    step "安装 Wine 组件（首次约 10~30 分钟，dotnet48 需要联网下载）"
    dim "组件：$PCLFE_WINETRICKS_VERBS"
    dim "如果长时间停在 'Executing w_do_call dotnet48'，多半是下载慢，可换网络后重试。"
    dim "详细输出会同时写入：$PCLFE_LOG_DIR/setup-*.log"

    _setup_log="$PCLFE_LOG_DIR/setup-$(timestamp).log"
    _status="$PCLFE_CACHE_DIR/.setup-status"
    {
        # winetricks 需要知道自己操作哪个 prefix。
        # shellcheck disable=SC2086
        WINEPREFIX="$PCLFE_PREFIX_DIR" WINEARCH=win64 \
        WINEDLLOVERRIDES="mscoree,mshtml=" W_OPT_UNATTENDED=1 \
            winetricks -q $PCLFE_WINETRICKS_VERBS 2>&1
        printf '%s' "$?" > "$_status"
    } | tee "$_setup_log"

    if [ "$(cat "$_status" 2>/dev/null || echo 1)" != "0" ]; then
        err "winetricks 安装组件失败（日志：$_setup_log）"
        wine32_hint || true
        dim "其他常见原因：无法访问 download.visualstudio.microsoft.com（可换网络/代理后重试）"
        dim "可尝试：pclfe prefix reset 后重跑 'pclfe setup'"
        return 1
    fi

    if dotnet48_installed; then
        date '+%Y-%m-%d %H:%M:%S' > "$PCLFE_COMPONENTS_STAMP"
        ok ".NET Framework 4.8 安装完成"
    else
        warn "组件已执行完毕，但未检测到 .NET Framework 4.8；启动器可能无法运行"
        dim "日志：$_setup_log"
        return 1
    fi
}

# 注册表微调：DPI 缩放、崩溃对话框、浏览器委派（正版登录的设备码流程会打开系统浏览器）。
prefix_tweaks() {
    prefix_exists || return 0
    _wine=$(wine_bin) || return 1

    _reg() {
        (
            export WINEPREFIX="$PCLFE_PREFIX_DIR" WINEDEBUG=-all
            "$_wine" reg add "$@" >/dev/null 2>&1
        ) || true
    }

    _reg 'HKCU\Control Panel\Desktop' /v LogPixels /t REG_DWORD /d "$PCLFE_DPI" /f
    _reg 'HKCU\Control Panel\Desktop' /v Win8DpiScaling /t REG_DWORD /d 1 /f
    _reg 'HKCU\Software\Wine\WineBrowser' /v Browsers /t REG_SZ /d 'xdg-open' /f
    _reg 'HKCU\Software\Wine\WineDbg' /v ShowCrashDialog /t REG_DWORD /d 0 /f
}

prefix_reset() {
    if [ -d "$PCLFE_PREFIX_DIR" ]; then
        _bak="$PCLFE_PREFIX_DIR.bak-$(timestamp)"
        mv "$PCLFE_PREFIX_DIR" "$_bak"
        ok "已把旧前缀移动到：$_bak"
    fi
    rm -f "$PCLFE_COMPONENTS_STAMP"
    prefix_create
}

prefix_status() {
    if prefix_exists; then
        log "前缀目录：$PCLFE_PREFIX_DIR"
        if dotnet48_installed; then
            log "  .NET Framework 4.8：已安装"
        else
            log "  .NET Framework 4.8：未检测到"
        fi
        if webview2_prefix_check; then
            log "  WebView2 运行时：已安装"
        else
            log "  WebView2 运行时：未安装（仅影响内置浏览器登录，见 pclfe doctor）"
        fi
    else
        log "前缀尚未创建，运行 'pclfe setup' 初始化"
    fi
}
